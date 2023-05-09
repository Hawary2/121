using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.Database
{
    public class AccountTable
    {
        public enum AccountState : byte
        {
            NotActivated = 100,
            ProjectManager = 255,
            GameHelper = 5,
            GameMaster = 3,
            Player = 2,
            Banned = 1,
            Cheat = 80,
            DoesntExist = 0
        }
        public class AccountRegister
        {
            public string Username = "";
            public string Password = "";
            public string Email = "";
            public AccountRegister()
            {
                Username = Password = Email = "";
            }
        }
        public class ChangePassword
        {
            public string Username = "";
            public string Password = "";
            public string Email = "";
            public ChangePassword()
            {
                Username = Password = "";
            }
        }
    }
}
