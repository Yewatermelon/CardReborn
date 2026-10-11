using Card.Presentation.Battle.Feedback;
using TMPro;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>
    /// 法力视图（M5-T2；FR-5.2）：渲染当前可用与上限两个字段。
    /// M14-T2（Docs/05 §5.5）：当前法力走 <see cref="NumberTween"/> 数值补间；上限立即刷新。
    /// </summary>
    public sealed class ManaView : MonoBehaviour
    {
        private readonly NumberTween _currentTween = new(NumberTween.DefaultDuration);

        [SerializeField] internal TMP_Text _currentText = null!;
        [SerializeField] internal TMP_Text _maxText = null!;

        public void SetData(int current, int max)
        {
            _currentTween.To(current);
            RefreshCurrent();
            _maxText.text = max.ToString();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>推进当前法力补间并刷新文本（EditMode 测试直接驱动）。</summary>
        internal void Tick(float dt)
        {
            if (!_currentTween.IsPlaying)
            {
                return;
            }

            _currentTween.Tick(dt);
            RefreshCurrent();
        }

        private void RefreshCurrent()
        {
            _currentText.text = _currentTween.Current.ToString();
        }
    }
}
