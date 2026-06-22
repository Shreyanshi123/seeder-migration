using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.Common
{
    public static class ExcelUtility
    {
        /// <summary>
        /// the function validate and return Date time formate.
        /// </summary>
        /// <param name="numberFormatId"> Represents the excel cell numberformatId</param>
        /// <returns></returns>
        public static string GetDateTimeFormat(uint numberFormatId)
        {
            return DateFormatDictionary.ContainsKey(numberFormatId) ? DateFormatDictionary[numberFormatId] : string.Empty;
        }
        /// <summary>
        /// the function check date time.
        /// </summary>
        /// <param name="date">Represents the date</param>
        /// <returns></returns>
        public static bool CheckDate(String date)
        {
            try
            {
               return DateTime.TryParseExact(date, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out DateTime dtDateTime);
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// the function check number.
        /// </summary>
        /// <param name="value">Represents the value : number string</param>
        /// <returns></returns>
        public static bool CheckNumber(String value)
        {
            try
            {
                if (int.TryParse(value, out int num) || decimal.TryParse(value, out decimal dec))
                    return true;
                else
                    return false;
            }
            catch
            {
                return false;
            }

        }
        /// <summary>
        /// the property contain datetime format to validate excel style numberfromatId array[] index value.
        /// </summary>
        private static readonly Dictionary<uint, string> DateFormatDictionary = new Dictionary<uint, string>()
        {
            [14] = "dd/MM/yyyy",
            [15] = "d-MMM-yy",
            [16] = "d-MMM",
            [17] = "MMM-yy",
            [18] = "h:mm AM/PM",
            [19] = "h:mm:ss AM/PM",
            [20] = "h:mm",
            [21] = "h:mm:ss",
            [22] = "M/d/yy h:mm",
            [30] = "M/d/yy",
            [34] = "yyyy-MM-dd",
            [45] = "mm:ss",
            [46] = "[h]:mm:ss",
            [47] = "mmss.0",
            [51] = "MM-dd",
            [52] = "yyyy-MM-dd",
            [53] = "yyyy-MM-dd",
            [55] = "yyyy-MM-dd",
            [56] = "yyyy-MM-dd",
            [58] = "MM-dd",
            [165] = "M/d/yy",
            [166] = "dd MMMM yyyy",
            [167] = "dd/MM/yyyy",
            [168] = "dd/MM/yy",
            [169] = "d.M.yy",
            [170] = "yyyy-MM-dd",
            [171] = "dd MMMM yyyy",
            [172] = "d MMMM yyyy",
            [173] = "M/d",
            [174] = "M/d/yy",
            [175] = "MM/dd/yy",
            [176] = "d-MMM",
            [177] = "d-MMM-yy",
            [178] = "dd-MMM-yy",
            [179] = "MMM-yy",
            [180] = "MMMM-yy",
            [181] = "MMMM d, yyyy",
            [182] = "M/d/yy hh:mm t",
            [183] = "M/d/y HH:mm",
            [184] = "MMM",
            [185] = "MMM-dd",
            [186] = "M/d/yyyy",
            [187] = "d-MMM-yyyy"
        };
    }
}
