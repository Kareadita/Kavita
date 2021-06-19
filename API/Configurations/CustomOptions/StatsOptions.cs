using System;

namespace API.Configurations.CustomOptions
{
    public class StatsOptions
    {
        public string ServerUrl { get; set; }
        public string ServerSecret { get; set; }
        public string SendDataAt { get; set; }

        private const char Separator = ':';

        public short SendDataHour => GetValueFromSendAt(0);
        public short SendDataMinute => GetValueFromSendAt(1);

        // The expected SendDataAt format is: Hour:Minute. Ex: 19:45
        private short GetValueFromSendAt(int index)
        {
            var key = $"{nameof(StatsOptions)}:{nameof(SendDataAt)}";

            if (string.IsNullOrEmpty(SendDataAt))
                throw new InvalidOperationException($"{key} is invalid. Check the app settings file");

            if (short.TryParse(SendDataAt.Split(Separator)[index], out var parsedValue))
                return parsedValue;

            throw new InvalidOperationException($"Could not parse {key}. Check the app settings file");
        }
    }
}