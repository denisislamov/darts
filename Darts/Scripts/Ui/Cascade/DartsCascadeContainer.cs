using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace Dip.Features.Darts.Ui
{
    public class DartsCascadeContainer : MonoBehaviour
    {
        [SerializeField] private DartsTaskCascade eventTaskSample;

        private readonly Stack<DartsTaskCascade> pool = new Stack<DartsTaskCascade>();
        private readonly List<DartsTaskCascade> taskViews = new List<DartsTaskCascade>();

        public DartsTaskCascade this[int index] => taskViews[index];

        public DartsTaskCascade AddTaskView()
        {
            var questTaskView = Allocate();
            taskViews.Add(questTaskView);
            return questTaskView;
        }

        public bool RemoveTaskView(DartsTaskCascade cascadeEventView)
        {
            if (cascadeEventView == null)
            {
                return false;
            }

            if (!taskViews.Remove(cascadeEventView))
            {
                return false;
            }
            Free(cascadeEventView);
            return true;
        }

        public void RemoveAllTaskViews()
        {
            for (int index = taskViews.Count - 1; index >= 0; --index)
            {
                RemoveTaskView(taskViews[index]);
            }
        }

        private DartsTaskCascade Allocate()
        {
            var cascadeEventView = pool.Any() ? pool.Pop() : default;
            while (!cascadeEventView && pool.Any())
            {
                cascadeEventView = pool.Any() ? pool.Pop() : default;
            }
            if (cascadeEventView)
            {
                cascadeEventView.gameObject.SetActive(true);
                return cascadeEventView;
            }
            cascadeEventView = Instantiate<DartsTaskCascade>(eventTaskSample, transform);
            cascadeEventView.gameObject.SetActive(true);
            return cascadeEventView;
        }

        private void Free(DartsTaskCascade cascadeEventView)
        {
            if (!cascadeEventView)
            {
                return;
            }

            cascadeEventView.gameObject.SetActive(false);
            pool.Push(cascadeEventView);
        }


        
    }
}