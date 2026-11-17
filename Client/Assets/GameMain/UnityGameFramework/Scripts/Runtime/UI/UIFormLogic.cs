//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using DataTable;
using GameFramework.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore.LowLevel;

namespace UnityGameFramework.Runtime
{
    [Serializable]
    public class TxtAndLanguageKey
    {
        public TextMeshProUGUI Tmp;
        public string LanguageKey;
    }
    [Serializable]
    public class ImageAndLanguagePrefix
    {
        public Image Target;
        [Tooltip("Sprite name before the language suffix, for example game_title.")]
        public string ImagePrefix;
        [Tooltip("Assign sprites named <prefix>_zh and <prefix>_en.")]
        public Sprite[] Sprites;

        public void Refresh(string languageCode)
        {
            if (Target == null || string.IsNullOrWhiteSpace(ImagePrefix) || Sprites == null) return;

            string prefix = ImagePrefix.Trim();
            string requestedName = prefix + "_" + languageCode;
            string fallbackName = prefix + "_zh";
            Sprite fallback = null;
            foreach (var sprite in Sprites)
            {
                if (sprite == null) continue;
                if (string.Equals(sprite.name, requestedName, StringComparison.Ordinal))
                {
                    Target.sprite = sprite;
                    return;
                }
                if (string.Equals(sprite.name, fallbackName, StringComparison.Ordinal))
                    fallback = sprite;
            }

            Target.sprite = fallback;
        }
    }

    /// <summary>
    /// 界面逻辑基类。
    /// </summary>
    public abstract class UIFormLogic : MonoBehaviour
    {
        private bool m_Available = false;
        private bool m_Visible = false;
        private UIForm m_UIForm = null;
        private Transform m_CachedTransform = null;
        private int m_OriginalLayer = 0;
        public List<TxtAndLanguageKey> AutoLanguageTmp;
        [SerializeField] private List<ImageAndLanguagePrefix> autoLanguageImages;
        /// <summary>
        /// 获取界面。
        /// </summary>
        public UIForm UIForm
        {
            get
            {
                return m_UIForm;
            }
        }

        /// <summary>
        /// 获取或设置界面名称。
        /// </summary>
        public string Name
        {
            get
            {
                return gameObject.name;
            }
            set
            {
                gameObject.name = value;
            }
        }

        /// <summary>
        /// 获取界面是否可用。
        /// </summary>
        public bool Available
        {
            get
            {
                return m_Available;
            }
        }

        /// <summary>
        /// 获取或设置界面是否可见。
        /// </summary>
        public bool Visible
        {
            get
            {
                return m_Available && m_Visible;
            }
            set
            {
                if (!m_Available)
                {
                    Log.Warning("UI form '{0}' is not available.", Name);
                    return;
                }

                if (m_Visible == value)
                {
                    return;
                }

                m_Visible = value;
                InternalSetVisible(value);
            }
        }

        /// <summary>
        /// 获取已缓存的 Transform。
        /// </summary>
        public Transform CachedTransform
        {
            get
            {
                return m_CachedTransform;
            }
        }

        /// <summary>
        /// 界面初始化。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnInit(object userData)
        {
            if (m_CachedTransform == null)
            {
                m_CachedTransform = transform;
            }

            m_UIForm = GetComponent<UIForm>();
            m_OriginalLayer = gameObject.layer;
            foreach (var button in GetComponentsInChildren<Button>(true)) BindButtonClickSound(button);
        }

        internal static void BindButtonClickSound(Button button)
        {
            button.onClick.RemoveListener(PlayButtonClick);
            button.onClick.AddListener(PlayButtonClick);
        }

        private static void PlayButtonClick() => HotGameEntry.Sound?.PlayUISound((int)EnumSound.BtnSfx);

        /// <summary>
        /// 界面回收。
        /// </summary>
        public virtual void OnRecycle()
        {
        }

        /// <summary>
        /// 界面打开。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnOpen(object userData)
        {
            RefreshLocalizedText();
            m_Available = true;
            Visible = true;
        }

        public virtual void RefreshLocalizedText()
        {
            var localization = GameEntry.Localization;
            if (AutoLanguageTmp != null)
            {
                foreach (var item in AutoLanguageTmp)
                {
                    if (item?.Tmp != null && !string.IsNullOrEmpty(item.LanguageKey))
                        item.Tmp.text = localization.GetString(item.LanguageKey.TrimEnd());
                }
            }

            if (autoLanguageImages == null) return;
            string languageCode = localization.Language == Language.English ? "en" : "zh";
            foreach (var item in autoLanguageImages)
                item?.Refresh(languageCode);
        }

        private static TMP_FontAsset chinesePaperFont;
        private static TMP_FontAsset englishPaperFont;

        private static TMP_FontAsset CreatePaperFont(string familyName, string windowsFileName)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), windowsFileName);
            if (File.Exists(path))
            {
                var font = TMP_FontAsset.CreateFontAsset(path, 0, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024);
                if (font != null) return font;
            }
#endif
            return TMP_FontAsset.CreateFontAsset(familyName, "Regular");
        }

        protected static void RefreshPaperTypography(Transform root)
        {
            if (root == null) return;
            bool english = GameEntry.Localization.Language == Language.English;
            var font = english
                ? (englishPaperFont ??= CreatePaperFont("Georgia", "georgia.ttf"))
                : (chinesePaperFont ??= CreatePaperFont("KaiTi", "simkai.ttf"));
            if (font == null) return;
            font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.font == font) continue;
                var previous = label.font;
                if (previous != null && previous != chinesePaperFont && previous != englishPaperFont &&
                    !font.fallbackFontAssetTable.Contains(previous))
                    font.fallbackFontAssetTable.Add(previous);
                label.font = font;
                label.fontSharedMaterial = font.material;
            }
        }

        public void Close()
        {
            GameEntry.UI.CloseUIForm(this);
        }

        /// <summary>
        /// 界面关闭。
        /// </summary>
        /// <param name="isShutdown">是否是关闭界面管理器时触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnClose(bool isShutdown, object userData)
        {
            gameObject.SetLayerRecursively(m_OriginalLayer);
            Visible = false;
            m_Available = false;
        }

        /// <summary>
        /// 界面暂停。
        /// </summary>
        public virtual void OnPause()
        {
            Visible = false;
        }

        /// <summary>
        /// 界面暂停恢复。
        /// </summary>
        public virtual void OnResume()
        {
            Visible = true;
        }

        /// <summary>
        /// 界面遮挡。
        /// </summary>
        public virtual void OnCover()
        {
        }

        /// <summary>
        /// 界面遮挡恢复。
        /// </summary>
        public virtual void OnReveal()
        {
        }

        /// <summary>
        /// 界面激活。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public virtual void OnRefocus(object userData)
        {
        }

        /// <summary>
        /// 界面轮询。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 界面深度改变。
        /// </summary>
        /// <param name="uiGroupDepth">界面组深度。</param>
        /// <param name="depthInUIGroup">界面在界面组中的深度。</param>
        public virtual void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
        }

        /// <summary>
        /// 设置界面的可见性。
        /// </summary>
        /// <param name="visible">界面的可见性。</param>
        public virtual void InternalSetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
    }
}
