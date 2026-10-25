using System.Collections.Generic;
using Planetcraft.Core;
using Planetcraft.Data;
using UnityEngine;

namespace Planetcraft.UI
{
    // Celebra marcos: banner grande + flash + arpejo ao alcançar um estágio,
    // banner menor + som curto quando novos projetos/receitas são desbloqueados.
    public class StageFeedback : MonoBehaviour
    {
        [SerializeField] GameDatabase database;
        [SerializeField] AudioSource audioSource;
        [SerializeField] float stageBannerDuration = 6f;
        [SerializeField] float unlockBannerDuration = 3.5f;
        [SerializeField, Range(0f, 1f)] float flashStrength = 0.35f;

        class Banner
        {
            public bool isStage;
            public string title;
            public string subtitle;
            public string body;
            public int frame;
        }

        readonly List<Banner> queue = new();
        Banner current;
        float shownAt;
        double lastTi;
        AudioClip chime;
        AudioClip blip;

        void Start()
        {
            chime = ProceduralAudio.StageChime();
            blip = ProceduralAudio.UnlockBlip();
            var tm = TerraformManager.Instance;
            if (tm == null) return;
            lastTi = tm.Ti;
            tm.StageReached += OnStageReached;
            tm.TiChanged += OnTiChanged;
        }

        void OnDestroy()
        {
            var tm = TerraformManager.Instance;
            if (tm == null) return;
            tm.StageReached -= OnStageReached;
            tm.TiChanged -= OnTiChanged;
        }

        void OnStageReached(TerraformStage stage)
        {
            queue.Add(new Banner
            {
                isStage = true,
                title = "NOVO ESTÁGIO",
                subtitle = stage.displayName,
                body = stage.description,
                frame = Time.frameCount,
            });
        }

        // O TerraformManager dispara StageReached antes de TiChanged no mesmo tick,
        // então desbloqueios do mesmo tick entram no banner do estágio.
        void OnTiChanged(double ti)
        {
            var unlocked = new List<string>();
            if (database != null && ti > lastTi)
            {
                foreach (var b in database.buildables)
                    if (b != null && b.requiredTi > lastTi && b.requiredTi <= ti) unlocked.Add(b.displayName);
                foreach (var r in database.recipes)
                    if (r != null && r.output != null && r.requiredTi > lastTi && r.requiredTi <= ti) unlocked.Add(r.output.displayName);
            }
            lastTi = ti;
            if (unlocked.Count == 0) return;

            string list = string.Join(", ", unlocked);
            var last = queue.Count > 0 ? queue[queue.Count - 1] : null;
            if (last != null && last.isStage && last.frame == Time.frameCount)
            {
                last.body += $"\n<color=#ffd27f>Desbloqueado: {list}</color>";
                return;
            }
            queue.Add(new Banner { title = "NOVO PROJETO", subtitle = list, frame = Time.frameCount });
        }

        void Update()
        {
            if (current != null && Time.unscaledTime - shownAt > Duration(current)) current = null;
            if (current != null || queue.Count == 0) return;

            current = queue[0];
            queue.RemoveAt(0);
            shownAt = Time.unscaledTime;
            if (audioSource != null) audioSource.PlayOneShot(current.isStage ? chime : blip);
        }

        float Duration(Banner b) => b.isStage ? stageBannerDuration : unlockBannerDuration;

        // Desenho fica no GameHud (UI Toolkit); aqui só a animação: fade, deslize e flash.
        void LateUpdate()
        {
            var hud = GameHud.Instance;
            if (hud == null) return;
            if (current == null)
            {
                hud.HideBanner();
                hud.SetFlash(0f);
                return;
            }

            float elapsed = Time.unscaledTime - shownAt;
            float alpha = Mathf.Clamp01(elapsed / 0.4f) * Mathf.Clamp01((Duration(current) - elapsed) / 0.8f);
            float slide = (1f - Mathf.Clamp01(elapsed / 0.4f)) * -30f;
            hud.ShowBanner(current.isStage, current.title, current.subtitle, current.body, alpha, slide);
            hud.SetFlash(current.isStage && elapsed < 0.9f ? flashStrength * (1f - elapsed / 0.9f) : 0f);
        }
    }
}
