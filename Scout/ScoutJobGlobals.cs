using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Colonisation.ScoutJob
{
    public class ScoutJobGlobals
    {

        public enum ScoutActivity
        {
            None,
            Walking,
            Scouting,
            Restocking,
            Fighting,
            Eating,
            SetUpCamp,
            Sleeping
        }
    }

    public static class ScoutJobHelper
    {
        public static void WriteLog(this string message)
        {
            if (Globals.DebugMode)
                //Log.Write(message);
                PhentrixGames.NewColonyAPI.Helpers.Utilities.WriteLog("Colonisation", message, Utilities.LogType.Error);
        }

        public const string JOB_ALIAS = "scoutjob";
        public const string JOB_STATION = "scoutrallypoint";
    }
}
