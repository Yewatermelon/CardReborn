using TMPro;
using UnityEngine;

namespace Card.Presentation.Battle
{
    /// <summary>法力视图（M5-T2；FR-5.2）：渲染当前可用与上限两个字段。</summary>
    public sealed class ManaView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text _currentText = null!;
        [SerializeField] internal TMP_Text _maxText = null!;

        public void SetData(int current, int max)
        {
            _currentText.text = current.ToString();
            _maxText.text = max.ToString();
        }
    }
}
