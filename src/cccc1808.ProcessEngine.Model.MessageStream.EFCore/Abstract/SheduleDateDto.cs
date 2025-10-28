using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Abstract
{
    // <param name="Date">UnixTimeMilliseconds</param>
    /// <param name="UpdateDate">UnixTimeMilliseconds</param>
    public readonly record struct SheduleDateDto
    {
        public DateTimeOffset Date { get; }
        public long DateUnixMiliseconds { get; }

        public SheduleDateDto(
            in DateTimeOffset date)
        {
            DateUnixMiliseconds = date.ToUnixTimeMilliseconds();
            Date = DateTimeOffset.FromUnixTimeMilliseconds(DateUnixMiliseconds);
        }

        public SheduleDateDto(
            long dateUnixMiliseconds)
        {
            DateUnixMiliseconds = dateUnixMiliseconds;
            Date = DateTimeOffset.FromUnixTimeMilliseconds(dateUnixMiliseconds);
        }

        public override int GetHashCode()
        {
            return DateUnixMiliseconds.GetHashCode();
        }
    }
}
