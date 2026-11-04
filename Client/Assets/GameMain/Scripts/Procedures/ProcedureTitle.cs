using DataTable;
using GameFramework.Procedure;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace Procedure
{
    // Cultivation sessions are opened by the title UI; the old auto-chess campaign is no longer entered.
    public class ProcedureTitle : ProcedureBase
    {
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            GameEntry.UI.OpenUIForm(UICtrlName.MainTitlePanel, "middle");
            HotGameEntry.HeroManager.InitStartCamera();
            HotGameEntry.Sound.PlayMusic((int)EnumSound.GameStartBGM);
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            var title = GameEntry.UI.GetUIForm(UICtrlName.MainTitlePanel);
            if (title != null) GameEntry.UI.CloseUIForm(title);
        }
    }
}
