using System;

namespace TestingPlatformLibrary
{
    public enum ConnectionType
    {
        VISA_USB = 0,
        VISA_GPIB = 1,
        VISA_SERIAL = 2,
        VXI_LAN = 3,
        RAW_SOCKET = 4,
        RAW_USBTMC = 5,
        RAW_SERIAL = 6,
        OTHER_RESERVED = 7,  // FOR INTERNAL USE ONLY 
    }

    public enum DeviceType
    {
        Function_Generator = 0,
        Oscilloscope = 1,
        Multimeter = 2,
        DC_Power_Supply = 3,
    }

    public interface ITestAndMeasurement
    {
        /// <summary>
        /// This function returns an identification string which must be unique to the instrument. This string loosely follows the VISA
        /// *IDN? response format. The identification string must be in the format "<Manufacturer>, <Model>, <Serial_Number>"
        /// even if the device is not connected over a VISA interface. The device must be on and connected for this function to work.
        /// </summary>
        /// <returns>An identification string unique to the device, in the format "<Manufacturer>, <Model>, <Serial_Number>"</returns>
        /// <exception cref="InvalidOperationException">thrown if the device is not on or not connected</exception>
        string GetIdentificationString();

        /// <summary>
        /// This function returns a string which is unique to the manufacturer and model of the instrument. The format of the string is
        /// "<Manufacturer>, <Model>". The device does not need to be initialized or connected for this to work. 
        /// </summary>
        /// <remarks>The purpose of this function is to allow for device implementation classes to have a way to return in a plaintext
        /// format what device they actually represent. The manufacturer and model values must be the same as in 
        /// <see cref="GetIdentificationString"/> The response string should likely be hard-coded as a constant in the implementation class
        /// </remarks>
        /// <returns>A string which is unique to the manufacturer and model of the instrument</returns>
        string GetModelString();

        /// <summary>
        /// Resets the instrument to its initial state (if possible).
        /// </summary>
        void Reset();

        /// <summary>
        /// Returns the way the current device is connected to the computer as a ConnectionType enum.
        /// </summary>
        /// <remarks>Currently this is more of a future proofing technique than anything remarkable. Results are currently undefined
        /// if the device is not on or not connected. (Alas since this field is hardcoded in all the current device implementations, it will
        /// likely not break anything)
        /// </remarks>
        /// <returns>the way the current device is connected to the computer as a ConnectionType enum</returns>
        ConnectionType GetConnectionType();

        /// <summary>
        /// Returns the device type of the device as a DeviceType enum.
        /// </summary>
        /// <returns>the device type of the device as a DeviceType enum</returns>
        DeviceType GetDeviceType();

    }
}
