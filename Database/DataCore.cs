using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;

namespace GameServer.Database
{
    public class DataCore
    {
        public static AtributesStatus AtributeStatus = new AtributesStatus();


        public static void LoadClient(Role.Player player)
        {
            player.Owner.Rune = new Role.Instance.Rune(player.Owner);
            player.Owner.Inventory = new Role.Instance.Inventory(player.Owner);
            player.Owner.Equipment = new Role.Instance.Equip(player.Owner);
            player.Owner.Warehouse = new Role.Instance.Warehouse(player.Owner);
            player.Owner.MyProfs = new Role.Instance.Proficiency(player.Owner);
            player.Owner.MyMail = new Role.Instance.Mail(player.Owner);
            player.Owner.MySpells = new Role.Instance.Spell(player.Owner);

            if (player.Owner.Achievement == null)
                player.Owner.Achievement = new AchievementCollection();

            player.Achievement = new Game.MsgServer.ClientAchievement(player.Owner.Achievement.Value, player.UID);

        }
    }
}
