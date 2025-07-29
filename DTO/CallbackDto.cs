using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZVSTelegramBot.DTO
{
    public class CallbackDto
    {
        public string Action { get; set; }

        public static CallbackDto FromString(string input)
        {
            Helper.ValidateString(input);

            var firstSeparator = input.IndexOf('|');
            return new CallbackDto
            {
                Action = firstSeparator == -1 ? input : input.Substring(0, firstSeparator)
            };
        }
        public override string ToString() => Action;
    }
}
