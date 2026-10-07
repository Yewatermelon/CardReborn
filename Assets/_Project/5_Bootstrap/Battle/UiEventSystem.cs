using UnityEngine;
using UnityEngine.EventSystems;

namespace Card.Bootstrap.Battle
{
    /// <summary>
    /// EventSystem 进程内单次创建（M6-T1）：用静态令牌保证主菜单→对战切场景不重复创建，
    /// 不使用 Find/FindAnyObjectByType（铁律 8），创建后 DontDestroyOnLoad。
    /// </summary>
    public static class UiEventSystem
    {
        private static bool s_created;

        public static void Ensure()
        {
            if (s_created)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(go);
            s_created = true;
        }
    }
}
