using System;

namespace SearchLego.DataSeeder.Common
{
    public interface ISettingValue
    {
        Jobs ReadParameterValue(string JobId, string FileName);
        bool WriteParameterValue(string JobId, string FileName, Action<Jobs> action);
    }
}