using TMPro;
using UnityEngine;

namespace Dip.Features.Darts
{
    public class DartsLoseReward : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;

        public void SetReward(string value)
        {
            text.text = value;
        }
    }
}