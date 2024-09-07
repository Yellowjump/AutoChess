using System.Collections.Generic;
using GameFramework.Fsm;
using UnityGameFramework.Runtime;

namespace Entity
{
    public partial class EntityQizi
    {
        private string _waitPlayAniList = string.Empty;
        private void UpdateAnimCommand()
        {
            if (animator!=null&&!string.IsNullOrEmpty(_waitPlayAniList))
            {
                animator.CrossFade(_waitPlayAniList,0.2f);
                _waitPlayAniList = string.Empty;
            }
        }

        public void AddAnimCommand(string aniName)
        {
            _waitPlayAniList = aniName;
        }
    }
}