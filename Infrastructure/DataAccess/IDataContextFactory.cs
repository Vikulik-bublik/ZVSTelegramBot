using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinqToDB.Data;
using System.Threading.Tasks;

namespace ZVSTelegramBot.Infrastructure.DataAccess
{
    public interface IDataContextFactory<TDataContext> where TDataContext : DataConnection
    {
        TDataContext CreateDataContext();
    }
}
