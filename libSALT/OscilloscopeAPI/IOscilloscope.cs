using System;

namespace libSALT.OscilloscopeAPI
{

    public enum TriggerStatus
    {
        Triggered = 1,
        Waiting = 2,
        Running = 3,
        Auto = 4,
        Stoppd = 5,
    }

    public interface IOscilloscope : ITestAndMeasurement
    {
        // Keep in mind that (at least as of yet), Getter commands that contain a channel parameter likely set the active channel to that parameter
        // EXCEPT GetYScale(channel). This thing is weird okay

        /// <summary>
        /// Grabs the Waveform data from the oscilloscope on the given channel in raw byte array form
        /// </summary>
        /// <param name="channel">the channel to grab the waveform data from</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        /// <returns>The waveform data in byte array form</returns>
        byte[] GetWaveData(int channel);

        /// <summary>
        /// Grabs the waveform data from the oscilloscope on the given channel in double array form. The doubles correspond to the actual voltages
        /// </summary>
        /// <remarks>This is more future proof and more abstracted, so the client doesn't have to know how to calculate the voltages from bytes</remarks>
        /// <param name="channel">The channel to download the waveform data from</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        /// <returns>The waveform data in double array form</returns>
        double[] GetWaveVoltages(int channel);

        /// <summary>
        /// Returns the number of input channels that the oscilloscope has
        /// </summary>
        /// <returns>the number of channels that the oscilloscope has</returns>
        int GetNumChannels();

        /// <summary>
        /// Starts the collection of data if it is stopped, unfreezing the display
        /// </summary>
        void Run();

        /// <summary>
        /// Stops the collection of data, freezing the oscilloscope display.
        /// </summary>
        void Stop();

        /// <summary>
        /// Enables the given channel, making it display on the oscilloscope screen
        /// </summary>
        /// <remarks>Keep in mind that for some oscilloscopes, enabling more channels reduces the effective sampling rate per channel</remarks>
        /// <throws></throws>
        /// <param name="channel">the channel to enable</param>
        void EnableChannel(int channel);

        /// <summary>
        /// Disables the given channel, making it no longer visible on the oscilloscope screen
        /// </summary>
        /// <remarks>Keep in mind that for some oscilloscpes, disabling channels increases the effective sampling rate per channel</remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        /// <param name="channel">The channel to disable</param>
        void DisableChannel(int channel);

        /// <summary>
        /// Returns true if the given channel is enabled, and false if it is not enabled
        /// </summary>
        /// <param name="channel">The channel to check if enabled</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        /// <returns>true if the given channel is enabled, false if it is not</returns>
        bool IsChannelEnabled(int channel);

        /// <summary>
        /// Equivalent to pushing the auto[scale] button on the oscilloscope. This automatically adjusts the vert. and horiz. scale, and trigger
        /// settings to optimize waveform display. If there is no auto[scale] feature on an oscilloscope, this function does nothing.
        /// <remarks>Make sure letting your users push this button remotely is something you really want.</remarks>
        /// </summary>
        void AutoScale();

        /// <summary>
        /// For speed purposes, it is not always wise to set the channel to query inside various functions, if each of them will be querying
        /// the same channel. This "active channel" will be the one the no parameter variations of Getter methods query when retrieving data.
        /// It is not neccesarily active as in enabled, or currently recieving data.
        /// </summary>
        /// <remarks>For example, for the DS1054z, this function writes the SCPI command :WAVeform:SOURce CHAN(channel#)
        /// setting the waveform source for data collection to the specific channel.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        /// <param name="channel">The channel to set the "active channel" to</param>
        void SetActiveChannel(int channel);

        /// <summary>
        /// Returns the currently active channel of the osclilloscope.
        /// </summary>
        /// <returns>The currently active osclilloscope channel</returns>
        int GetActiveChannel();

        /// <summary>
        /// Returns the minimum voltage scale that this oscilloscope supports in volts
        /// </summary>
        /// <returns>the minimum voltage scale that this oscilloscope supports in volts</returns>
        double GetMinimumVoltageScale();

        /// <summary>
        /// The highest and lowest possible offset values are found by multiplying the current Y/Voltage scale by this constant, and then by 1 or -1 respectively to get 
        /// the max and min offset values
        /// </summary>
        /// <remarks>For the DS1054z returns 8</remarks>
        /// <returns>The scale constant for voltage offset</returns>
        double GetYAxisOffsetScaleConstant();

        /// <summary>
        /// The highest and lowest possible trigger voltage values are found by multiplying the current Y/Voltage scale by this constant, and then by 1 or -1 respectively to get 
        /// the max and min offset values
        /// </summary>
        /// <remarks>For the DS1054z returns 5</remarks>
        /// <returns>The scale constant for trigger position</returns>
        double GetTriggerPositionScaleConstant();

        /// <summary>
        /// The highest and lowest possible x-axis/time offset values are found by multiplying the current time/div scale by this constant, and then by 1 or -1 respectively to get 
        /// the max and min offset values
        /// </summary>
        /// <remarks>For the DS1054z returns 10</remarks>
        /// <returns>The scale constant for x-axis/time position</returns>
        double GetXAxisOffsetScaleConstant();

        /// <summary>
        /// Returns an array of voltage scale (double) values that are used as "presets" for the scope, such as 1V, 0.5v 10mV etc.  
        /// </summary>
        /// <returns>an array of voltage scale (double) values that are used as "presets" for the scope</returns>
        double[] GetVoltageScalePresets();

        /// <summary>
        /// Returns an array of strings containing the text representation of the double values stored in GetVoltageScalePresets().
        /// For example, if GetVoltageScalePresets()[2] is .05, GetVoltageScalePresetStrings()[2] is 5mV.
        /// </summary>
        /// <returns>an array of strings containing the text representation of the double values stored in GetVoltageScalePresets()</returns>
        string[] GetVoltageScalePresetStrings();

        /// <summary>
        /// Returns an array of time scale (double) values that are used as "presets" for the scope, such as 1.00ms, 1s, 20s, 1ns etc.  
        /// </summary>
        /// <returns>an array of time scale (double) values that are used as "presets" for the scope</returns>
        double[] GetTimeScalePresets();

        /// <summary>
        /// Returns an array of strings containing the text representation of the double values stored in GetTimeScalePresets().
        /// For example, if GetTimeScalePresets()[2] is 2, GetVoltageScalePresetStrings()[2] is 2s.
        /// </summary>
        /// <returns>an array of strings containing the text representation of the double values stored in GetTimeScalePresets()</returns>
        string[] GetTimeScalePresetStrings();

        /// <summary>
        /// Returns the minimum time scale that this oscilloscope supports in seconds
        /// </summary>
        /// <returns>the minimum time scale that this oscilloscope supports in seconds</returns>
        double GetMinimumTimeScale();

        /// <summary>
        /// Returns the maximum voltage scale that this oscilloscope supports in volts
        /// </summary>
        /// <returns>the maximum voltage scale that this oscilloscope supports in volts</returns>
        double GetMaximumVoltageScale();

        /// <summary>
        /// Returns the maximum time scale that this oscilloscope supports in seconds
        /// </summary>
        /// <returns>the maximum time scale that this oscilloscope supports in seconds</returns>
        double GetMaximumTimeScale();

        /// <summary>
        /// Retrieves the y-axis voltage scale of the scope (for the given channel) and returns it as a double
        /// </summary>
        /// <param name="channel">The channel to get the y-axis voltage scale from</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        /// <returns>the y axis voltage scale of the scope for the given channel</returns>
        double GetYScale(int channel);

        /// <summary>
        /// Retrieves the y-axis voltage scale of the scope (for the active channel) and returns it as a double
        /// </summary>
        /// <returns>The y-axis voltage scale of the scope for the active channel</returns>
        double GetYScale();

        /// <summary>
        /// Sets the y-axis voltage scale of the scope for the given channel
        /// </summary>
        /// <param name="channel">The channel to set the y-axis voltage scale of</param>
        /// <param name="voltageScale">The scale to set it to, in volts</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        void SetYScale(int channel, double voltageScale);

        /// <summary>
        /// Retrieves the y-axis increment for the given channel, this is related to the Y scale by different factors depending
        /// on the scope.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        /// <param name="channel">The channel to get the increment from</param>
        /// <returns>The y-axis increment for the given channel</returns>
        double GetYIncrement(int channel);

        /// <summary>
        /// Retrieves the x-axis increment for the given channel, this is related to the x scale by different factors depending
        /// on the scope and data read mode.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        /// <param name="channel">The channel to get the increment from</param>
        /// <returns>The x-axis increment for the given channel</returns>
        double GetXIncrement(int channel);

        /// <summary>
        /// Retrieves the x-axis increment for the given channel, this is related to the x scale by different factors depending
        /// on the scope and data read mode.
        /// </summary>
        /// <returns>The x-axis increment for the active</returns>
        double GetXIncrement();

        /// <summary>
        /// Retrieves the y-axis increment for the active channel, this is related to the Y scale by different factors depending
        /// on the scope.
        /// </summary>
        /// <returns>the y-axis increment for the active channel</returns>
        double GetYIncrement();

        ///// <summary>
        ///// Retrieves the vertical offset relative to the vertical reference position of the specified channel 
        ///// </summary>
        ///// <param name="channel">The channel to retrieve the vertical offset from</param>
        ///// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        ///// <returns>The vertical offset of the given channel</returns>
        //double GetYOrigin(int channel);

        ///// <summary>
        ///// Retrieves the vertical offset relative to the vertical reference position of the active channel 
        ///// </summary>
        ///// <returns>The vertical offset of the active channel</returns>
        //double GetYOrigin();

        ///// <summary>
        ///// Retrieves the vertical reference position of the specified channel in the Y direction
        ///// </summary>
        ///// <param name="channel">the channel to retrieve the vertical reference from</param>
        ///// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        ///// <returns>The vertical reference of the specified channel</returns>
        //double GetYReference(int channel);

        ///// <summary>
        ///// Retrieves the vertical reference position of the active channel in the Y direction
        ///// </summary>
        ///// <returns>The vertical reference of the active channel</returns>
        //double GetYReference();

        /// <summary>
        /// Gets the current XAxis/time scale in seconds that the scope is set to.
        /// </summary>
        /// <returns>The time scale in seconds/div of the scope</returns>
        double GetXAxisScale();

        /// <summary>
        /// Sets the X-Axis/time scale of the scope for the given channel (if appropriate)
        /// </summary>
        /// <param name="timeScale">The time scale, in seconds, to change the time scale to</param>
        void SetXAxisScale(double timeScale);

        /// <summary>
        /// Gets the vertical offset of the waveform display for the given channel in volts.
        /// </summary>
        /// <param name="channel">The channel to get the vertical offset from</param>
        /// <returns>The vertical offset of the display (in volts) for the given channel</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist.</exception>
        double GetYAxisOffset(int channel);

        /// <summary>
        /// Sets the vertical offset of the waveform display for the given channel.
        /// </summary>
        /// <param name="channel">The channel to set the vertical offset of</param>
        /// <param name="offset">The vertical offset to set for the given channel, in volts</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist 
        /// or if the offset is out of range</exception>
        void SetYAxisOffset(int channel, double offset);

        /// <summary>
        /// Gets the horizontal position/time offset for the scope.
        /// </summary>
        /// <returns>The horizontal position/time offset for the scope</returns>
        double GetXAxisOffset();

        /// <summary>
        /// Sets the position (x-axis)/time offset for the scope.
        /// </summary>
        /// <param name="offset">The position offset to set </param>
        void SetXAxisOffset(double offset);

        /// <summary>
        /// Returns the level in V of the trigger line as a double
        /// </summary>
        /// <returns> the level in V of the trigger line as a double></returns>
        double GetTriggerLevel();

        /// <summary>
        /// Sets the level of the oscilloscope's trigger to the passed in double
        /// </summary>
        /// <param name="voltageLevel">The voltage to set the trigger voltage to</param>
        void SetTriggerLevel(double voltageLevel);

        /// <summary>
        /// Returns the current memory depth of the scope, in number of waveform points saved per trigger sample
        /// returns 0 if the scope is set to auto memdepth.
        /// </summary>
        /// <remarks>On some oscilloscopes (at least the DS1000 range), the memory depth decreases as more channels are
        /// enabled. It can also return AUTO, and if that is the case we will return 0.
        /// </remarks>
        /// <returns>the current memory depth of the scope, in number of waveform points saved per trigger sample or 0 if
        /// if the scope is set to auto.
        /// </returns>
        int GetMemDepth();

        /// <summary>
        /// Sets the memory depth of the scope in number of waveform points saved per trigger sample.
        /// if points is 0 then the memory depth is set to AUTO. The number must be one of the allowed memory depths of the scope,
        /// retrieved from <see cref="GetAllowedMemDepths"/>
        /// </summary>
        /// <param name="points">The number of points saved per trigger sample to set the scope's memory depth to. If 0 then 
        /// memory depth is set to AUTO.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the number of points to set is not one of the allowed memory depths</exception>
        void SetMemDepth(int points);

        /// <summary>
        /// Returns an array of allowed memory depths for the scope. These can and will change when different number of channels are enabled
        /// and disabled.
        /// </summary>
        /// <returns>An int array of allowed memory depths for the scope</returns>
        int[] GetAllowedMemDepths();

        /// <summary>
        /// This function is the equivalent of pressing the "single" button on the oscilloscope, it sets the trigger mode to single, and
        /// stops the display on trigger
        /// </summary>
        void Single();
        
        /// <summary>
        /// Retrieves the deep memory waveform data from the scope in raw byte array form
        /// </summary>
        /// <param name="channel">The channel to get the deep memory waveform data from</param>
        /// <remarks>Even though the data returned is in raw byte array form, metadata such as headers and 0xA terminators are/must be removed before returning</remarks>
        /// <returns>The deep memory waveform data from the requested channel in byte[] form</returns>
        /// <exception cref="ArgumentException">Thrown if the oscilloscope memory depth is set to AUTO</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel does not exist on the oscilloscope</exception>
        byte[] GetDeepMemData(int channel);

        /// <summary>
        /// Retrieves the deep memory waveform data from the scope in double/voltage array form
        /// </summary>
        /// <param name="channel">The channel to get the deep memory waveform data from</param>
        /// <returns>The deep memory waveform data from the requested channel in double[] form</returns>
        /// <exception cref="ArgumentException">Thrown if the oscilloscope memory depth is set to AUTO</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel does not exist on the oscilloscope</exception>
        double[] GetDeepMemVoltages(int channel);

        /// <summary>
        /// Returns the requested channel's waveform trace color
        /// </summary>
        /// <remarks>To have the traces drawn on external displays match the scope as much as possible, there should be a way to match
        /// the colors.
        /// </remarks>
        /// <param name="channel">The channel to request the color of</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        /// <returns>The waveform trace color of the requested channel</returns>
        System.Drawing.Color GetChannelColor(int channel);

        /// <summary>
        /// Returns the number of vertical divisions (number of boxes from top to bottom of screen) represented on the oscillscope screen.
        /// The crosshair is assumed to be at division halfway down the screen, and halfway across the screen as well
        /// </summary>
        /// <returns>The number of vertical divisions of the oscilloscope</returns>
        int GetNumVerticalDivisions();

        /// <summary>
        /// Returns the number of horizontal divisions (number of boxes from left to right of screen) represented on the oscillscope screen.
        /// The crosshair is assumed to be at division halfway across the screen, and halfway down the screen as well
        /// </summary>
        /// <returns>The number of horizontal divisions of the oscilloscope</returns>
        int GetNumHorizontalDivisions();

        /// <summary>
        /// Returns the ideal number of osciilscope points captured from the screen for updating remote displays. For example, this value is 1200 on the
        /// Rigol DS1054z. If an oscilloscope does not have this sort of feature, this function returns -1. 
        /// This value should likely be a constant in a implementation class, it should never change during runtime.
        /// </summary>
        /// <returns>The ideal number of oscilloscope points captured from the screen</returns>
        int GetNumPointsPerScreenCapture();

        /// <summary>
        /// Returns the status of the oscilloscope trigger as a TriggerStatus enum. 
        /// </summary>
        /// <returns></returns>
        TriggerStatus GetTriggerStatus();
    }
}
