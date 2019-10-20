using System;
using System.Drawing;

namespace TestingPlatformLibrary.OscilloscopeAPI
{
    public abstract class VISAOscilloscope : VISADevice, IOscilloscope
    {
        protected int numChannels;  // the number of channels that this oscilloscope has
        protected static readonly object threadLock = new object();

        protected VISAOscilloscope(string visaID, int numChannels)
            : base(visaID)
        {
            this.numChannels = numChannels;  // set the number of output channels that this function generator has
        }

        public int GetNumChannels()
        {
            return numChannels;
        }

        public override DeviceType GetDeviceType()
        {
            return DeviceType.Oscilloscope;
        }

        public static ConnectedDeviceStruct<VISAOscilloscope> GetConnectedOscilloscopes()
        {
            return GetConnectedDevices<VISAOscilloscope>();
        }

        protected void CheckChannelParam(int channel)
        {
            if (channel > GetNumChannels() || channel < 1)  // throw an exception if the channel being asked for is
                                                            // higher than what the oscilloscope has, or if it's less than 1.
            {
                throw new ArgumentOutOfRangeException(
                    "The requested output channel does not exist on this oscilloscope. " +
                    "The highest channel on this scope is: " + GetNumChannels() + ", and the channel requested was: " + channel);
            }
        }

        public abstract byte[] GetWaveData(int channel);
        public abstract double[] GetWaveVoltages(int channel);
        public abstract void Run();
        public abstract void Stop();
        public abstract void EnableChannel(int channel);
        public abstract void DisableChannel(int channel);
        public abstract bool IsChannelEnabled(int channel);
        public abstract void AutoScale();
        public abstract void SetActiveChannel(int channel);
        public abstract int GetActiveChannel();
        public abstract double GetMinimumVoltageScale();
        public abstract double GetYAxisOffsetScaleConstant();
        public abstract double GetTriggerPositionScaleConstant();
        public abstract double GetXAxisOffsetScaleConstant();
        public abstract double[] GetVoltageScalePresets();
        public abstract string[] GetVoltageScalePresetStrings();
        public abstract double[] GetTimeScalePresets();
        public abstract string[] GetTimeScalePresetStrings();
        public abstract double GetMinimumTimeScale();
        public abstract double GetMaximumVoltageScale();
        public abstract double GetMaximumTimeScale();
        public abstract double GetYScale(int channel);
        public abstract double GetYScale();
        public abstract void SetYScale(int channel, double voltageScale);
        public abstract double GetYIncrement(int channel);
        public abstract double GetXIncrement(int channel);
        public abstract double GetXIncrement();
        public abstract double GetYIncrement();
        public abstract double GetXAxisScale();
        public abstract void SetXAxisScale(double timeScale);
        public abstract double GetYAxisOffset(int channel);
        public abstract void SetYAxisOffset(int channel, double offset);
        public abstract double GetXAxisOffset();
        public abstract void SetXAxisOffset(double offset);
        public abstract double GetTriggerLevel();
        public abstract void SetTriggerLevel(double voltageLevel);
        public abstract int GetMemDepth();
        public abstract void SetMemDepth(int points);
        public abstract int[] GetAllowedMemDepths();
        public abstract void Single();
        public abstract byte[] GetDeepMemData(int channel);
        public abstract double[] GetDeepMemVoltages(int channel);
        public abstract Color GetChannelColor(int channel);
        public abstract int GetNumVerticalDivisions();
        public abstract int GetNumHorizontalDivisions();
    }
}
