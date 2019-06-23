using PhentrixGames.NewColonyAPI;
using PhentrixGames.NewColonyAPI.Classes;
using UnityEngine;

namespace Colonisation.Commands
{
    [AttributeCommand]
    class TeleportXYZ : BaseChatCommand
    {
        public TeleportXYZ() : base("/tp", "", "/tp <playername> X Y Z") { }

        protected override bool RunCommand(Players.Player id, string args)
        {
            Pipliz.Log.Write(args);

            char[] separators = new char[] { ' ' };

            string[] argsArray = args.Split(separators);

            Players.Player targetPlayer;

            if (Players.TryMatchName(argsArray[1], out targetPlayer))
            {
                Pipliz.Log.Write("No player found for name {" + argsArray[1] + "}");
                return false;
            }

            if (!int.TryParse(argsArray[2], out int x) ||
                !int.TryParse(argsArray[3], out int y) ||
                !int.TryParse(argsArray[4], out int z))
            {
                Pipliz.Log.Write("Invalid coordinates");
                return false;
            }

            targetPlayer.Position = new Vector3(x, y, z);

            return true;
        }

        public bool IsCommand(string chat)
        {
            return true;
        }
    }
}
