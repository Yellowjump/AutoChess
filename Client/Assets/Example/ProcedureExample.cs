using GameFramework;
using GameFramework.Procedure;
using GameFramework.UI;
using UnityEngine;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace GameFrameworkExample
{
    public class ProcedureExample : ProcedureBase
    {
        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            string welcomeMessage = Utility.Text.Format("Hello! This is an empty project based on Game Framework {0}.", Version.GameFrameworkVersion);
            Log.Info(welcomeMessage);
            /*Log.Warning(welcomeMessage);
            Log.Error(welcomeMessage);*/
            GameEntry.UI.OpenUIForm(UICtrlName.TestUIPrefab,"middle");
            /*var asset = AssetDatabase.LoadAssetAtPath(UICtrlName.TestUIPrefab,typeof(GameObject));
            var pa = GameObject.Find("UI Group - middle");
            GameObject.Instantiate(asset, pa.transform);*/
        }
    }
}
