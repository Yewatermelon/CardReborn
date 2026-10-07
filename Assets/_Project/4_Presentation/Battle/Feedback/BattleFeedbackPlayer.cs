using Card.Core;
using Card.Domain.Match;
using UnityEngine;

namespace Card.Presentation.Battle.Feedback
{
    /// <summary>
    /// 反馈播放器（M5-T6；FR-8.2）：把权威事件流（12 类 <see cref="GameEvent"/>）映射为
    /// 浮动数字、死亡淡出、回合/终局横幅与音效钩子。四类反馈独立开关，整体可加速
    /// （<see cref="FeedbackSettings"/> 引用共享、逐事件读取）。
    /// 只消费事件，不读不改游戏状态（03 §5.6）。
    /// </summary>
    public sealed class BattleFeedbackPlayer
    {
        internal const float FloatingTextDuration = 1.2f;
        internal const float DeathFadeDuration = 0.8f;
        internal const float BannerDuration = 1.6f;

        internal static readonly Color DamageColor = new Color(1f, 0.25f, 0.25f);
        internal static readonly Color HealColor = new Color(0.3f, 1f, 0.3f);

        private readonly IFeedbackTargetLocator _locator;
        private readonly FloatingTextPool _textPool;
        private readonly Transform _textParent;
        private readonly TurnBannerView _banner;
        private readonly IAudioCuePlayer _audio;
        private readonly FeedbackSettings _settings;
        private readonly int _localSeat;
        private MatchEventPump? _pump;

        public BattleFeedbackPlayer(
            IFeedbackTargetLocator locator,
            FloatingTextPool textPool,
            Transform floatingTextParent,
            TurnBannerView banner,
            IAudioCuePlayer audio,
            FeedbackSettings settings,
            int localSeat)
        {
            _locator = Guard.NotNull(locator, nameof(locator));
            _textPool = Guard.NotNull(textPool, nameof(textPool));
            _textParent = Guard.NotNull(floatingTextParent, nameof(floatingTextParent));
            _banner = Guard.NotNull(banner, nameof(banner));
            _audio = Guard.NotNull(audio, nameof(audio));
            _settings = Guard.NotNull(settings, nameof(settings));
            _localSeat = localSeat;
        }

        /// <summary>订阅事件泵；重复调用先退订旧泵。</summary>
        public void Bind(MatchEventPump pump)
        {
            Guard.NotNull(pump, nameof(pump));
            Unbind();
            _pump = pump;
            _pump.EventAppended += Handle;
        }

        public void Unbind()
        {
            if (_pump != null)
            {
                _pump.EventAppended -= Handle;
                _pump = null;
            }
        }

        /// <summary>
        /// 事件 → 反馈映射。PhaseChangedEvent / TurnEndedEvent 显式无反馈
        /// （阶段流转由 TurnStarted 横幅承载），其余每类事件至少产出视觉或音效反馈。
        /// </summary>
        public void Handle(GameEvent e)
        {
            Guard.NotNull(e, nameof(e));

            switch (e)
            {
                case DamageEvent damage:
                    PlayDamage(damage);
                    break;
                case HealingEvent healing:
                    PlayHealing(healing);
                    break;
                case FatigueEvent fatigue:
                    PlayFatigue(fatigue);
                    break;
                case CardDeathEvent death:
                    PlayDeath(death);
                    break;
                case TurnStartedEvent turn:
                    PlayTurnStarted(turn);
                    break;
                case MatchEndedEvent ended:
                    PlayMatchEnded(ended);
                    break;
                case CardPlayedEvent:
                    Cue(AudioCue.CardPlayed);
                    break;
                case AttackDeclaredEvent:
                    Cue(AudioCue.AttackDeclared);
                    break;
                case CardDrawnEvent:
                    Cue(AudioCue.CardDrawn);
                    break;
                case CardBurnedEvent:
                    Cue(AudioCue.CardBurned);
                    break;
            }
        }

        private void PlayDamage(DamageEvent damage)
        {
            FloatAtTarget(damage.TargetInstanceId, damage.TargetHeroSeat, "-" + damage.Amount, DamageColor);
            Cue(AudioCue.DamageDealt);
        }

        private void PlayHealing(HealingEvent healing)
        {
            FloatAtTarget(healing.TargetInstanceId, healing.TargetHeroSeat, "+" + healing.Amount, HealColor);
            Cue(AudioCue.HealingReceived);
        }

        private void PlayFatigue(FatigueEvent fatigue)
        {
            FloatAtTarget(null, fatigue.Seat, "-" + fatigue.Damage, DamageColor);
            Cue(AudioCue.FatigueDamage);
        }

        private void PlayDeath(CardDeathEvent death)
        {
            if (_settings.DeathFadeEnabled
                && _locator.TryGetMinionView(death.CardInstanceId, out CardView? view))
            {
                CardFadeOutView fade = view!.GetComponent<CardFadeOutView>();
                if (fade == null)
                {
                    fade = view!.gameObject.AddComponent<CardFadeOutView>();
                }

                fade.Play(_settings.ScaleDuration(DeathFadeDuration));
            }

            Cue(AudioCue.MinionDeath);
        }

        private void PlayTurnStarted(TurnStartedEvent turn)
        {
            ShowBanner(turn.ActiveSeat == _localSeat ? "你的回合" : "对手回合");
            Cue(AudioCue.TurnStarted);
        }

        private void PlayMatchEnded(MatchEndedEvent ended)
        {
            string text;
            AudioCue cue;
            if (ended.Result == MatchResult.Draw)
            {
                text = "平局";
                cue = AudioCue.MatchDraw;
            }
            else if (ended.WinnerId == _localSeat)
            {
                text = "胜利！";
                cue = AudioCue.Victory;
            }
            else
            {
                text = "败北";
                cue = AudioCue.Defeat;
            }

            ShowBanner(text);
            Cue(cue);
        }

        private void FloatAtTarget(int? targetInstanceId, int? targetHeroSeat, string content, Color color)
        {
            if (!_settings.DamageNumbersEnabled)
            {
                return;
            }

            Vector3 position = default;
            bool found = targetInstanceId.HasValue
                ? _locator.TryGetMinionAnchor(targetInstanceId.Value, out position)
                : targetHeroSeat.HasValue && _locator.TryGetHeroAnchor(targetHeroSeat.Value, out position);
            if (!found)
            {
                return;
            }

            FloatingTextView view = _textPool.Rent(_textParent);
            view.transform.position = position;
            view.Show(content, color, _settings.ScaleDuration(FloatingTextDuration));
        }

        private void ShowBanner(string content)
        {
            if (_settings.TurnBannerEnabled)
            {
                _banner.Show(content, _settings.ScaleDuration(BannerDuration));
            }
        }

        private void Cue(AudioCue cue)
        {
            if (_settings.AudioEnabled)
            {
                _audio.Play(cue);
            }
        }
    }
}
