// * Created by AccServer
// * Copyright © 2020-2021
// * AccServer - Project

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace AccServer.Database
{
    using MYSQLCOMMAND = MySql.Data.MySqlClient.MySqlCommand;
    using MYSQLREADER = MySql.Data.MySqlClient.MySqlDataReader;
    using MYSQLCONNECTION = MySql.Data.MySqlClient.MySqlConnection;

    public unsafe static class DataHolder
    {
        private static string ConnectionString = "";
        public static void CreateConnection(string database, string password)
        {
            ConnectionString = "Server=" + "localhost" + ";Port=3306;Database=" + database + ";Uid=" + "root" + ";Password=" + password + ";Persist Security Info=True;Pooling=true; Min Pool Size = 32;  Max Pool Size = 300;";
        }
        public static MYSQLCONNECTION   MySqlConnection
        {
            get
            {
                MYSQLCONNECTION conn = new MYSQLCONNECTION();
                conn.ConnectionString = ConnectionString;
                return conn;
            }
        }
    }
}