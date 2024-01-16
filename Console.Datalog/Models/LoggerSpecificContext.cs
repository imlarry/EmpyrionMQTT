﻿using ESB.Messaging;
using ESBLog.Database;

namespace ESBLog.Common
{
    public class LoggerSpecificContext : BaseContextData
    {
        public LoggerSpecificContext()
        {
        }
        public DbAccess? DBconnection { get; set; }

    }
}