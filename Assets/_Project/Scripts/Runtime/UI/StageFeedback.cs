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
        GUIStyle titleStyle;
        GUIStyle subtitleStyle;
        GUIStyle bodyStyle;

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

        void OnGUI()
        {
            if (current == null) return;
            EnsureStyles();

            float elapsed = Time.unscaledTime - shownAt;
            float duration = Duration(current);
            float alpha = Mathf.Clamp01(elapsed / 0.4f) * Mathf.Clamp01((duration - elapsed) / 0.8f);
            var previousColor = GUI.color;

            if (current.isStage && elapsed < 0.9f)
            {
                GUI.color = new Color(1f, 0.97f, 0.9f, flashStrength * (1f - elapsed / 0.9f));
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            }

            float width = Mathf.Min(640f, Screen.width - 40f);
            float height = current.isStage ? 150f : 74f;
            float slide = (1f - Mathf.Clamp01(elapsed / 0.4f)) * -30f;
            var rect = new Rect((Screen.width - width) / 2f, 90f + slide, width, height);

            GUI.color = new Color(0f, 0f, 0f, 0.65f * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);

            var inner = new Rect(rect.x + 16, rect.y + 10, rect.width - 32, rect.height - 20);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 20), current.title, titleStyle);
            GUI.Label(new Rect(inner.x, inner.y + 20, inner.width, current.isStage ? 44 : 30), current.subtitle, subtitleStyle);
            if (!string.IsNullOrEmpty(current.body))
                GUI.Label(new Rect(inner.x, inner.y + 66, inner.width, inner.height - 66), current.body, bodyStyle);

            GUI.color = previousColor;
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.6f, 0.85f, 1f) },
            };
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 30, fontStyle = FontStyle.Bold, wordWrap = true,
                normal = { textColor = Color.white },
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter, fontSize = 14, wordWrap = true, richText = true,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
            };
        }
    }
}
