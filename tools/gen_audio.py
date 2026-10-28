"""Gera a trilha e a ambiência de Return to Earth por síntese (numpy), sem amostras de terceiros: obra original do projeto.

Uso (ambiente do bpy, que já tem numpy; precisa de `soundfile` para gravar OGG Vorbis):
    python tools/gen_audio.py [nome ...]

Saída em Assets/_Project/Resources/Audio/{Music,Ambience}/*.ogg:
  Music:    music_dead (Terra morta), music_hope (despertar), music_alive (Terra viva) — ~2,5 min, em loop sem emenda
  Ambience: wind_loop, hum_loop (zumbido de máquina), bird_01..06 (cantos curtos para os estágios de vida)
"""
import os
import sys

import numpy as np
import soundfile as sf

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Audio")
SR = 32000
RNG = np.random.default_rng(2026)


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def env_adsr(n, a, d, s, r):
    """Envelope ADSR com tempos em amostras; `s` é o nível de sustentação."""
    a, d, r = int(a), int(d), int(r)
    sus = max(0, n - a - d - r)
    e = np.concatenate([np.linspace(0, 1, a, endpoint=False), np.linspace(1, s, d, endpoint=False),
                        np.full(sus, s), np.linspace(s, 0, r)])
    return np.pad(e, (0, max(0, n - len(e))))[:n]


def pad_voice(freq, seconds, brightness=5, detune=0.004, attack=2.5, release=3.5, voices=3):
    """Voz de pad: soma aditiva de harmônicos com queda 1/h^1.4, três vozes levemente desafinadas."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for v in range(voices):
        f = freq * (1 + detune * (v - (voices - 1) / 2))
        phase = RNG.uniform(0, 2 * np.pi)
        for h in range(1, brightness + 1):
            out += np.sin(2 * np.pi * f * h * t + phase * h) / h ** 1.4
    return out / voices * env_adsr(n, attack * SR, 0.5 * SR, 0.85, release * SR)


def pluck(freq, seconds, decay=2.2, bright=7):
    """Nota percussiva (piano elétrico simples): harmônicos com decaimento exponencial mais rápido nos agudos."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for h in range(1, bright + 1):
        out += np.sin(2 * np.pi * freq * h * t) * np.exp(-t * decay * (1 + 0.6 * h)) / h
    return out * np.minimum(1, t * 400)


def bell(freq, seconds=6.0):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for ratio, amp in ((1, 1), (2.76, 0.5), (5.4, 0.25), (8.93, 0.12)):
        out += amp * np.sin(2 * np.pi * freq * ratio * t) * np.exp(-t * (0.9 + ratio * 0.5))
    return out * np.minimum(1, t * 300)


def place(buf, clip, at, gain=1.0, pan=0.0):
    """Mistura `clip` em `buf` (estéreo) no instante `at` (s), com pan -1..1 (potência constante)."""
    i = int(at * SR)
    if i >= len(buf):
        return
    j = min(len(buf), i + len(clip))
    seg = clip[: j - i] * gain
    l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
    buf[i:j, 0] += seg * l
    buf[i:j, 1] += seg * r


def filtered_noise(seconds, low, high):
    """Ruído branco limitado a [low, high] Hz por filtro em frequência (FFT)."""
    n = int(seconds * SR)
    spec = np.fft.rfft(RNG.normal(size=n))
    f = np.fft.rfftfreq(n, 1 / SR)
    mask = np.clip((f - low) / max(1.0, low * 0.5), 0, 1) * np.clip((high - f) / max(1.0, high * 0.5), 0, 1)
    out = np.fft.irfft(spec * mask, n)
    return out / (np.abs(out).max() + 1e-9)


def reverb(stereo, seconds=3.2, wet=0.35):
    """Reverb por convolução com ruído de decaimento exponencial (canais decorrelacionados), via FFT."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    out = np.zeros_like(stereo)
    size = len(stereo) + n
    fft_len = 1 << (size - 1).bit_length()
    for c in range(2):
        ir = RNG.normal(size=n) * np.exp(-t * 3.0 / seconds * 2.2)
        ir[:int(0.02 * SR)] *= np.linspace(0, 1, int(0.02 * SR))
        ir /= np.sqrt((ir ** 2).sum())
        conv = np.fft.irfft(np.fft.rfft(stereo[:, c], fft_len) * np.fft.rfft(ir, fft_len), fft_len)[: len(stereo)]
        out[:, c] = conv
    return stereo * (1 - wet) + out * wet


def make_loop(stereo, tail_seconds):
    """Torna o trecho um loop sem emenda: o rabo (depois de `len - tail`) é somado, em fade, ao início."""
    tail = int(tail_seconds * SR)
    body, rest = stereo[:-tail], stereo[-tail:]
    fade = np.linspace(0, 1, tail)[:, None]
    body[:tail] = body[:tail] * fade + rest * (1 - fade) ** 1.0 * 1.0
    return body


def normalize(stereo, peak=0.85):
    stereo = stereo - stereo.mean(axis=0)
    return stereo / (np.abs(stereo).max() + 1e-9) * peak


def save(name, stereo, folder, quality=0.5):
    path = os.path.join(OUT, folder)
    os.makedirs(path, exist_ok=True)
    full = os.path.join(path, name + ".ogg")
    data = stereo.astype(np.float32)
    # Em blocos: o libvorbis do Windows estoura a pilha ao receber o arquivo inteiro de uma vez.
    with sf.SoundFile(full, "w", samplerate=SR, channels=2, format="OGG", subtype="VORBIS") as f:
        for i in range(0, len(data), 8192):
            f.write(data[i:i + 8192])
    print(f"  {os.path.relpath(full, ROOT)}  {os.path.getsize(full) / 1024:.0f} KB  {len(stereo) / SR:.0f}s")


# --------------------------------------------------------------------------------------------------- trilha
def score(seconds, chords, root_pad, bright, arp=None, bells=None, pulse=None, tail=8.0, wind=0.0, seed=1):
    """Monta uma peça: acordes em pad (troca a cada `chord_len`), arpejo opcional, sinos esparsos e pulso grave."""
    global RNG
    RNG = np.random.default_rng(seed)
    total = seconds + tail
    buf = np.zeros((int(total * SR), 2))
    chord_len = seconds / len(chords)
    for k, notes in enumerate(chords):
        at = k * chord_len
        for i, n in enumerate(notes):
            clip = pad_voice(midi(n), chord_len + 5.0, brightness=bright, attack=3.0, release=4.5)
            place(buf, clip, at, 0.22, pan=(i - len(notes) / 2) * 0.35)
        place(buf, pad_voice(midi(chords[k][0] - 12), chord_len + 5.0, brightness=3, attack=3.5, release=5), at, 0.32)
    if arp:
        scale, step, gain = arp
        for k, notes in enumerate(chords):
            at0 = k * chord_len
            pool = [n + o for n in notes for o in (12, 24)] if scale is None else scale
            t = at0 + 0.5
            while t < at0 + chord_len - 1.0:
                n = int(RNG.choice(pool))
                place(buf, pluck(midi(n), 3.5), t, gain * RNG.uniform(0.6, 1.0), pan=RNG.uniform(-0.7, 0.7))
                t += step * RNG.choice([1, 1, 2, 3])
    if bells:
        scale, density, gain = bells
        t = 4.0
        while t < seconds - 3:
            place(buf, bell(midi(int(RNG.choice(scale))), 7.0), t, gain * RNG.uniform(0.5, 1.0), pan=RNG.uniform(-0.9, 0.9))
            t += RNG.exponential(density)
    if pulse:
        interval, gain = pulse
        t = 2.0
        while t < seconds - 2:
            n = int(0.9 * SR)
            tt = np.arange(n) / SR
            kick = np.sin(2 * np.pi * (46 + 40 * np.exp(-tt * 18)) * tt) * np.exp(-tt * 5)
            place(buf, kick, t, gain)
            t += interval
    if wind:
        w = filtered_noise(total, 120, 900)
        slow = 0.5 + 0.5 * np.sin(2 * np.pi * t_axis(total) / 23.0)
        buf[:, 0] += w * slow * wind
        buf[:, 1] += np.roll(w, 4000) * (1 - slow * 0.6) * wind
    buf = reverb(buf, 3.6, 0.38)
    return normalize(make_loop(buf, tail))


def music_dead():
    # Ré menor sem terça fixa: sensação de vazio. Acordes lentos, sinos raros, vento.
    chords = [[38, 45, 50, 57], [34, 41, 50, 53], [36, 43, 50, 55], [38, 45, 52, 57], [34, 41, 48, 53], [31, 38, 50, 55]]
    return score(150, chords, 38, 4, bells=([74, 77, 81, 86, 69], 9.0, 0.22), wind=0.12, seed=11)


def music_hope():
    # Fá lídio: o primeiro verde. Pad mais claro, arpejo suave, sinos mais frequentes.
    chords = [[41, 48, 52, 57, 64], [43, 50, 55, 59, 66], [45, 52, 57, 60], [38, 45, 50, 57, 62], [41, 48, 53, 60],
              [36, 43, 52, 59]]
    scale = [65, 69, 72, 76, 79, 81, 84, 88]
    return score(160, chords, 41, 6, arp=(scale, 1.7, 0.16), bells=([77, 81, 84, 88, 93], 5.5, 0.16), wind=0.05, seed=22)


def music_alive():
    # Dó maior / lá menor: a Terra viva. Arpejos fluentes, pulso grave discreto, pad quente.
    chords = [[36, 43, 52, 55, 60], [33, 40, 52, 57, 60], [41, 48, 57, 60, 65], [43, 50, 55, 59, 62], [36, 43, 52, 55, 64],
              [33, 40, 48, 57, 64], [41, 48, 57, 64, 69], [43, 50, 59, 62, 67]]
    scale = [60, 62, 64, 67, 69, 72, 74, 76, 79, 81, 84]
    return score(176, chords, 36, 7, arp=(scale, 0.9, 0.2), bells=([84, 88, 91, 93], 8.0, 0.1), pulse=(2.8, 0.18),
                 seed=33)


# ------------------------------------------------------------------------------------------------ ambiência
def wind_loop(seconds=24.0):
    global RNG
    RNG = np.random.default_rng(5)
    n = int(seconds * SR)
    t = np.arange(n) / SR
    base = filtered_noise(seconds, 80, 700)
    gust = 0.55 + 0.45 * np.sin(2 * np.pi * t / seconds * 2 + 1.2) * np.sin(2 * np.pi * t / seconds * 3)
    hiss = filtered_noise(seconds, 1500, 5200) * (0.18 + 0.22 * np.clip(gust - 0.5, 0, 1))
    left = base * gust + hiss
    right = np.roll(base, 3000) * (1.1 - gust * 0.5) + np.roll(hiss, 2200)
    stereo = np.stack([left, right], axis=1)
    fade = int(1.5 * SR)  # emenda do loop: cruza o final com o início
    head, tail = stereo[:fade].copy(), stereo[-fade:].copy()
    ramp = np.linspace(0, 1, fade)[:, None]
    stereo[:fade] = head * ramp + tail * (1 - ramp)
    return normalize(stereo[:-fade], 0.8)


def hum_loop(seconds=6.0):
    """Zumbido de máquina: fundamental 55 Hz com harmônicos pares, leve batimento e ruído filtrado."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for h, amp in ((1, 1.0), (2, 0.55), (3, 0.3), (4, 0.22), (6, 0.1), (8, 0.06)):
        out += amp * np.sin(2 * np.pi * 55 * h * t + 0.3 * h) * (1 + 0.04 * np.sin(2 * np.pi * (3 + h * 0.1) * t))
    out += 0.08 * filtered_noise(seconds, 200, 1400)
    out *= 1 + 0.06 * np.sin(2 * np.pi * 1 / seconds * 2 * t)
    stereo = np.stack([out, np.roll(out, 90)], axis=1)
    return normalize(stereo, 0.7)


def bird(seed):
    """Canto curto: 2 a 5 notas de FM com glissando; cada semente é uma "ave" diferente."""
    r = np.random.default_rng(seed)
    notes = int(r.integers(2, 6))
    base = r.uniform(2200, 4200)
    out = np.zeros(int(0.22 * SR * notes + 0.3 * SR))
    pos = 0
    for k in range(notes):
        dur = r.uniform(0.07, 0.16)
        n = int(dur * SR)
        tt = np.arange(n) / SR
        f0 = base * r.uniform(0.8, 1.3)
        glide = r.uniform(-0.35, 0.5)
        freq = f0 * (1 + glide * tt / dur)
        mod = np.sin(2 * np.pi * freq * r.uniform(1.5, 3.0) * tt) * r.uniform(0.5, 2.5)
        tone = np.sin(2 * np.pi * np.cumsum(freq) / SR + mod * 0.4)
        out[pos:pos + n] += tone * np.sin(np.pi * tt / dur) ** 0.7 * 0.6
        pos += int((dur + r.uniform(0.04, 0.12)) * SR)
    out = out[: pos + int(0.05 * SR)]
    stereo = np.stack([out, out], axis=1)
    return normalize(reverb(np.pad(stereo, ((0, int(1.2 * SR)), (0, 0))), 1.4, 0.3), 0.7)


JOBS = {
    "music_dead": ("Music", music_dead), "music_hope": ("Music", music_hope), "music_alive": ("Music", music_alive),
    "wind_loop": ("Ambience", wind_loop), "hum_loop": ("Ambience", hum_loop),
    **{f"bird_{i:02d}": ("Ambience", (lambda i=i: bird(100 + i * 7))) for i in range(1, 7)},
}


def main():
    names = sys.argv[1:] or list(JOBS)
    for name in names:
        folder, fn = JOBS[name]
        print(name)
        save(name, fn(), folder)


if __name__ == "__main__":
    main()
