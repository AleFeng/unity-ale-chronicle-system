#if ATK_TMP
using UiText = TMPro.TMP_Text;
#else
using UiText = UnityEngine.UI.Text;
#endif

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Ale.Chronicle.Runtime.UI;
using Ale.Effect;

namespace Ale.Chronicle.Demo
{
    /// <summary>
    /// 效果系统演示控制（仅用于 Demo）：一列按钮驱动「使用技能 → 施加效果」「直接施加效果」「推进世界时钟」「重置」，
    /// 状态行显示世界日 / 活动效果 / 运行时特质与最近一次操作结果；角色面板 <see cref="UiwCharacterView"/> 订阅运行时事件自动刷新。
    ///
    /// <para>按钮引用留空时按子节点名查找（Btn_PsychicTamper / Btn_WarCry / Btn_MindWard / Btn_RegenDraught /
    /// Btn_Advance30 / Btn_AdvanceYear / Btn_Reset；状态行 Status），点击回调在 Awake 经代码绑定，无需在 Inspector 配置 OnClick。
    /// 技能 / 效果 id 与 CharacterSystemSeeder D7 一致。</para>
    /// </summary>
    public class ChronicleEffectDemo : MonoBehaviour
    {
        // ── 与 Seeder D7 一致的 id ──────────────────────────────────────────────
        private const string SkillPsychicTamper = "psychic_tamper";   // 精神篡改 → 效果 mind_tamper
        private const string SkillWarCry        = "war_cry";          // 战意     → 效果 battle_focus
        private const string SkillMindWard      = "mind_ward";        // 心智护盾 → 效果 mental_ward
        private const string EffectRegenDraught = "regen_draught";    // 回复药剂（直接施加；I2 由道具「使用」触发）

        [Header("目标")]
        [Tooltip("操作的角色 id（与角色面板一致）。")]
        public string characterId = "luna";
        [Tooltip("是否把效果线索（施加 / 结算 / 移除）打到 Console。")]
        public bool logCues = true;

        [Header("按钮（留空则按子节点名查找）")]
        [SerializeField] private Button btnPsychicTamper;
        [SerializeField] private Button btnWarCry;
        [SerializeField] private Button btnMindWard;
        [SerializeField] private Button btnRegenDraught;
        [SerializeField] private Button btnAdvance30;
        [SerializeField] private Button btnAdvanceYear;
        [SerializeField] private Button btnReset;

        [Header("状态行（留空则找子节点 Status）")]
        [SerializeField] private UiText statusText;

        private string _lastNote = string.Empty;
        private bool   _subscribed;

        private void Awake()
        {
            btnPsychicTamper = Resolve(btnPsychicTamper, "Btn_PsychicTamper");
            btnWarCry        = Resolve(btnWarCry,        "Btn_WarCry");
            btnMindWard      = Resolve(btnMindWard,      "Btn_MindWard");
            btnRegenDraught  = Resolve(btnRegenDraught,  "Btn_RegenDraught");
            btnAdvance30     = Resolve(btnAdvance30,     "Btn_Advance30");
            btnAdvanceYear   = Resolve(btnAdvanceYear,   "Btn_AdvanceYear");
            btnReset         = Resolve(btnReset,         "Btn_Reset");
            if (statusText == null)
            {
                var st = transform.Find("Status");
                if (st) statusText = st.GetComponentInChildren<UiText>(true);
            }

            Wire(btnPsychicTamper, UsePsychicTamper);
            Wire(btnWarCry,        UseWarCry);
            Wire(btnMindWard,      UseMindWard);
            Wire(btnRegenDraught,  DrinkRegenDraught);
            Wire(btnAdvance30,     Advance30Days);
            Wire(btnAdvanceYear,   AdvanceOneYear);
            Wire(btnReset,         ResetRuntime);
        }

        private void OnEnable()
        {
            var em = EffectRuntimeManager.Instance;
            if (logCues && em.CueSink == null) em.CueSink = new ChronicleDebugCueSink();
            if (!_subscribed)
            {
                _subscribed = true;
                em.OnEffectsChanged                        += OnChanged;
                TraitRuntimeManager.Instance.OnTraitsChanged += OnChanged;
                ChronicleClock.Instance.OnDaysAdvanced      += OnDaysAdvanced;
                SkillRuntimeManager.Instance.OnSkillUsed    += OnSkillUsed;
            }
            RefreshStatus();
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            EffectRuntimeManager.Instance.OnEffectsChanged -= OnChanged;
            TraitRuntimeManager.Instance.OnTraitsChanged   -= OnChanged;
            ChronicleClock.Instance.OnDaysAdvanced         -= OnDaysAdvanced;
            SkillRuntimeManager.Instance.OnSkillUsed       -= OnSkillUsed;
        }

        // ── 操作 ────────────────────────────────────────────────────────────────

        /// <summary>使用「精神篡改」：施加 mind_tamper → 授予临时特质「精神异常」（3 年）。</summary>
        public void UsePsychicTamper() => UseSkill(SkillPsychicTamper);

        /// <summary>使用「战意」：施加 battle_focus → 战力 +10 持续 30 天（重复施加刷新）。</summary>
        public void UseWarCry() => UseSkill(SkillWarCry);

        /// <summary>使用「心智护盾」：施加 mental_ward → 免疫精神类效果（之后的精神篡改被阻断）。</summary>
        public void UseMindWard() => UseSkill(SkillMindWard);

        /// <summary>饮用「回复药剂」：直接施加 regen_draught → 5 天内每天耐力 +5 永久落地。</summary>
        public void DrinkRegenDraught()
        {
            var r = EffectRuntimeManager.Instance.Apply(EffectRegenDraught, characterId);
            Note($"回复药剂 → {Outcome(r)}");
        }

        public void Advance30Days()
        {
            ChronicleClock.Instance.AdvanceDays(30);
            Note("推进 30 天");
        }

        public void AdvanceOneYear()
        {
            ChronicleClock.Instance.AdvanceYears(1);
            Note("推进 1 年");
        }

        /// <summary>清空该角色的活动效果 / 永久落地 / 运行时特质，并把世界日归零。</summary>
        public void ResetRuntime()
        {
            EffectRuntimeManager.Instance.ClearCharacter(characterId);
            TraitRuntimeManager.Instance.ClearRuntime(characterId);
            ChronicleClock.Instance.SetWorldDay(0);
            Note("已重置运行时状态");
        }

        private void UseSkill(string skillId)
        {
            if (!SkillRuntimeManager.Instance.UseSkill(characterId, skillId, "demo"))
                Note($"技能 '{skillId}' 不存在（请先运行 Character Seeder D7）");
            // 结果由 OnSkillUsed 回调写入状态行
        }

        // ── 事件 / 状态 ─────────────────────────────────────────────────────────

        private void OnChanged(string id)
        {
            if (id == characterId) RefreshStatus();
        }

        private void OnDaysAdvanced(int days, int day) => RefreshStatus();

        private void OnSkillUsed(SkillUseEvent e)
        {
            if (e.TargetCharacterId != characterId) return;
            var skill = ChronicleDataManager.Instance.GetSkill(e.SkillId);
            string name = skill != null ? UiwSkillText.ResolveName(skill) : e.SkillId;
            var sb = new StringBuilder();
            sb.Append("使用「").Append(name).Append("」：").Append(e.EffectsApplied).Append('/').Append(e.EffectResults.Count).Append(" 个效果生效");
            if (e.EffectResults.Count > 0)
            {
                sb.Append("（");
                for (int i = 0; i < e.EffectResults.Count; i++)
                {
                    if (i > 0) sb.Append("，");
                    sb.Append(Outcome(e.EffectResults[i]));
                }
                sb.Append("）");
            }
            Note(sb.ToString());
        }

        private void Note(string note)
        {
            _lastNote = note;
            Debug.Log("[ChronicleEffectDemo] " + note);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (!statusText) return;
            var clock = ChronicleClock.Instance;
            var em    = EffectRuntimeManager.Instance;
            var tm    = TraitRuntimeManager.Instance;

            var sb = new StringBuilder();
            sb.Append("第 ").Append(clock.WorldDay).Append(" 天（").Append(ChronicleClock.ToYears(clock.WorldDay)).Append(" 年）")
              .Append("  ·  活动效果 ").Append(em.GetActiveEffects(characterId).Count)
              .Append("  ·  运行时特质 ").Append(tm.GetRuntimeTraits(characterId).Count);
            if (!string.IsNullOrEmpty(_lastNote)) sb.Append('\n').Append(_lastNote);
            statusText.text = sb.ToString();
        }

        private static string Outcome(EffectApplyResult r)
        {
            switch (r.Outcome)
            {
                case EEffectApplyOutcome.Applied:                  return "已施加";
                case EEffectApplyOutcome.Stacked:                  return "已叠加";
                case EEffectApplyOutcome.Refreshed:                return "已刷新";
                case EEffectApplyOutcome.BlockedByImmunity:        return "被免疫阻断";
                case EEffectApplyOutcome.BlockedByTagRequirements: return "标签要求不满足";
                case EEffectApplyOutcome.BlockedByCondition:       return "条件不满足";
                case EEffectApplyOutcome.BlockedByChance:          return "概率未命中";
                default:                                           return r.Outcome.ToString();
            }
        }

        // ── 绑定辅助 ────────────────────────────────────────────────────────────

        private Button Resolve(Button assigned, string childName)
        {
            if (assigned) return assigned;
            var t = transform.Find(childName);
            return t ? t.GetComponent<Button>() : null;
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button) button.onClick.AddListener(action);
        }
    }
}
