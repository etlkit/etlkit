namespace EtlKit.Common.DataFlow
{
    /// <summary>
    /// Contains static information which affects all Dataflow tasks in EtlKit.
    /// Here you can set the threshold value when information about processed records should appear.
    /// </summary>
    public static class DataFlow
    {
        public static int? LoggingThresholdRows { get; set; } = 1000;
        public static bool HasLoggingThresholdRows => LoggingThresholdRows is > 0;

        /// <summary>
        /// Set all settings back to default (which is null or false)
        /// </summary>
        public static void ClearSettings()
        {
            LoggingThresholdRows = 1000;
        }
    }
}
