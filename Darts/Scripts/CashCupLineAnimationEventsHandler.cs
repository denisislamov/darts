using System;
using UnityEngine;

namespace Dip.Features.CashCup
{
    public class CashCupLineAnimationEventsHandler : MonoBehaviour
    {
        public Action onRotatedFromFirst;
        //Animation event
        public void OnRotatedFromFirst()
        {
            onRotatedFromFirst?.Invoke();
            onRotatedFromFirst = null;
        }
        public Action onRotatedToFirst;
        //Animation event
        public void OnRotatedToFirst()
        {
            onRotatedToFirst?.Invoke();
            onRotatedToFirst = null;
        }
    }
}