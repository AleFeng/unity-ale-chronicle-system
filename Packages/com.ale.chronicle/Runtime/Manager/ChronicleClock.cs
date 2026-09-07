using System;
using System.Collections.Generic;
using Ale.Toolkit.Runtime;

namespace Ale.Chronicle
{
    /// <summary>世界时钟存档状态（单条：当前世界日）。</summary>
    [Serializable]
    public class ChronicleClockState
    {
        public int worldDay;
    }

    /// <summary>
    /// 世界时钟：以「世界日」为唯一时间单位（年龄 / 临时特质时效 / 效果时长与周期统一按天计）。
    /// <see cref="AdvanceDays"/> 推进后依次驱动 <see cref="TraitRuntimeManager.Tick"/>（临时特质到期）与
    /// <see cref="EffectRuntimeManager.Tick"/>（效果周期结算 / 到期），最后派发 <see cref="OnDaysAdvanced"/>。
    /// 「日 → 岁」换算暂按 <see cref="DaysPerYear"/>，历法 / 种族细化随后续子系统落地。
    /// </summary>
    public class ChronicleClock : ToolkitSingleton<ChronicleClock>, ISaveable<ChronicleClockState>
    {
        /// <summary>每年天数（换算用）。</summary>
        public const int DaysPerYear = 365;

        /// <summary>当前世界日（从 0 起）。</summary>
        public int WorldDay { get; private set; }

        /// <summary>推进后派发：(本次推进天数, 推进后的世界日)。</summary>
        public event Action<int, int> OnDaysAdvanced;

        private bool _advancing;

        protected override void Init() { }

        /// <summary>直接设置世界日（不触发推进；用于开局 / 读档校准）。负数夹到 0。</summary>
        public void SetWorldDay(int worldDay) => WorldDay = worldDay < 0 ? 0 : worldDay;

        /// <summary>推进 <paramref name="days"/> 天（≤0 忽略）：特质到期 → 效果周期 / 到期 → <see cref="OnDaysAdvanced"/>。推进过程中再入被忽略。</summary>
        public void AdvanceDays(int days)
        {
            if (days <= 0) return;
            if (_advancing)
            {
                UnityEngine.Debug.LogWarning("[ChronicleClock] 推进过程中再次推进被忽略（请勿在 Tick / OnDaysAdvanced 回调内推进时钟）。");
                return;
            }

            _advancing = true;
            try
            {
                WorldDay += days;
                TraitRuntimeManager.Instance.Tick(days);
                EffectRuntimeManager.Instance.Tick(days);
                OnDaysAdvanced?.Invoke(days, WorldDay);
            }
            finally
            {
                _advancing = false;
            }
        }

        /// <summary>按年推进（<paramref name="years"/> × <see cref="DaysPerYear"/> 天）。</summary>
        public void AdvanceYears(int years) => AdvanceDays(years * DaysPerYear);

        /// <summary>天数 → 整岁（向下取整）。</summary>
        public static int ToYears(int days) => days / DaysPerYear;

        #region 存档

        public List<ChronicleClockState> GetSaveData()
            => new List<ChronicleClockState> { new ChronicleClockState { worldDay = WorldDay } };

        public void LoadSaveData(List<ChronicleClockState> data)
        {
            WorldDay = 0;
            if (data == null) return;
            foreach (var st in data)
                if (st != null) { SetWorldDay(st.worldDay); break; }
        }

        public void ResetAll() => WorldDay = 0;

        #endregion
    }
}
