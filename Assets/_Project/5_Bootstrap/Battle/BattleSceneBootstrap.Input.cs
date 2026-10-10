using Card.Domain.Match;
using Card.Presentation.Battle;
using Card.Presentation.Battle.Targeting;
using UnityEngine;

namespace Card.Bootstrap.Battle
{
    public sealed partial class BattleSceneBootstrap
    {
        private PendingIntent _pendingIntent;
        private int _pendingCardId;

        private enum PendingIntent
        {
            None = 0,
            PlayCard = 1,
            HeroPower = 2,
        }

        private void OnHandCardClicked(int instanceId)
        {
            if (InputBlocked()) return;
            _pendingIntent = PendingIntent.PlayCard;
            _pendingCardId = instanceId;
            _input!.NotifyHandCardClicked(instanceId);
        }

        private void OnBoardMinionClicked(int attackerInstanceId)
        {
            if (InputBlocked()) return;
            _targeting!.BeginAttackTargeting(attackerInstanceId, () => CardScreenPosition(attackerInstanceId));
        }

        private void OnHeroPowerClicked()
        {
            if (InputBlocked()) return;
            _pendingIntent = PendingIntent.HeroPower;
            _input!.NotifyHeroPowerClicked();
        }

        private void OnEndTurnClicked()
        {
            if (InputBlocked()) return;
            _sink!.Submit(new EndTurnCommand(_controller!.View.ActivePlayerId));
        }

        private bool InputBlocked() => _isPve && _aiRunner is { IsAiTurn: true };

        private void OnInputRejected(CommandError error)
        {
            if (error == CommandError.TargetRequired)
            {
                BeginTargetingForPendingIntent();
            }

            _pendingIntent = PendingIntent.None;
        }

        private void BeginTargetingForPendingIntent()
        {
            switch (_pendingIntent)
            {
                case PendingIntent.PlayCard:
                    int cardId = _pendingCardId;
                    _targeting!.BeginPlayCardTargeting(cardId, () => CardScreenPosition(cardId));
                    break;
                case PendingIntent.HeroPower:
                    _targeting!.BeginHeroPowerTargeting(HeroPowerScreenPosition);
                    break;
            }
        }

        private Vector2 CardScreenPosition(int instanceId)
        {
            if (_ui != null
                && (_ui.LocalHand.TryGetCardView(instanceId, out CardView? view)
                    || _ui.LocalBoard.TryGetCardView(instanceId, out view))
                && view != null)
            {
                return RectTransformUtility.WorldToScreenPoint(null, view.transform.position);
            }

            return Vector2.zero;
        }

        private Vector2 HeroPowerScreenPosition()
        {
            return _ui == null
                ? Vector2.zero
                : RectTransformUtility.WorldToScreenPoint(null, _ui.HeroPowerButton.transform.position);
        }
    }
}
