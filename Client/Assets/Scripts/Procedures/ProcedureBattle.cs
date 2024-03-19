using DataTable;
using GameFramework;
using GameFramework.Procedure;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
namespace Procedure
{
    public class ProcedureBattle:ProcedureBase
    {
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            string welcomeMessage = Utility.Text.Format("Hello! This is an empty project based on Game Framework {0}.", Version.GameFrameworkVersion);
            Log.Info(welcomeMessage);
            /*Log.Warning(welcomeMessage);
            Log.Error(welcomeMessage);*/
            GameEntry.UI.OpenUIForm(UICtrlName.JieMianUIPrefab, "middle");
            var assetPath = GameEntry.DataTable.GetDataTable<DRAssetsPath>("AssetsPath");
            if (assetPath.HasDataRow(101))
            {
                Log.Info("assetID 101 path is :"+assetPath[101].AssetPath);
            }
            
        }
    }
}