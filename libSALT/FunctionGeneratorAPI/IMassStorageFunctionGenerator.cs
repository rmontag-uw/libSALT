using System;

namespace TestingPlatformLibrary.FunctionGeneratorAPI
{
    /*
     * The IUSBFunctionGenerator Interface provides an interface for function generators that have USB mass storage
     * capabilities, and sane memory management capabilities. This interface provides a way to standardize the sometimes
     * odd ways function generators handle USB memory management, i.e. sometimes returning file structures as strings,
     * or having no exception handling at all for reading non-existent files. It is up to the implementor to take heed
     * of these idiosyncrasies, and make the best of them.
     * 
     * EDIT: This interface was originally called IUSBFunctionGenerator, it was for function generators that had a USB connector on the front
     * that could be used as USB mass storage. However, the one I worked with when creating the testing platform didn't have the ability to delete
     * files with SCPI commands, so this functionality was left out. I'm tempted to delete this interface, but it could be used for internal
     * storage stuff as well I suppose. I'm not going to try to add those features though unless needed.
     */
    interface IMassStorageFunctionGenerator : IFunctionGenerator
    {
        /*
         * Returns true if there is a USB Mass Storage Device connected to the FG, and false if there is not.
         */
        /// <summary>
        /// Returns true if there is a USB Mass Storage Device connected to the function generator
        /// and false if there is not.
        /// </summary>
        /// <returns>if there is a USB Mass Storage Device connected to the function generator</returns>
        bool USBConnected();

        /*
         * Changes the working directory to the one with the name specified in the parameter
         * Must throw a DirectoryNotFoundException if the directory is not found
         * To implement this interface correctly, the parameter ".." must change the working directory to the one above
         * the current directory in the file tree, if one exists. (cd in linux)
         */
        /// <summary>
        /// Changes the working directory to the one with the name specified.
        /// </summary>
        /// <remarks>".." must change the directory to the parent directory of the current directory, if one exists</remarks>
        /// <exception cref="System.IO.DirectoryNotFoundException">Thrown if the requested directory does not exist</exception>
        /// <param name="directoryName">Name of the directory to change to</param>
        void ChangeDirectory(string directoryName);

        /*
         * Creates a new directory on the USB device with the name directoryName.
         */
        /// <summary>
        /// Creates a new directory on the USB device with the name directoryName.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the program is unable to create the directory</exception>
        /// <param name="directoryName">Name of the directory to create</param>
        void MakeDirectory(string directoryName);

        /*
         * Loads the waveform data from the file with the name given in the parameter 
         * into the given channel's active memory.
         * 
         * throws a FileNotFoundException if the requested file does not exist.
         * throws an ArgumentOutOfRangeException if the requested channel does not exist.
         */
        /// <summary>
        /// Loads the waveform data from the file with the name given in the parameter 
        /// into the given channel's active memory.
        /// </summary>
        /// <remarks>The file type is not specified here, it will likely depend on 
        /// the make/model of the function generator</remarks>
        /// <exception cref="System.IO.FileNotFoundException">Thrown if the requested file does not exist</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel does not exist</exception>
        /// <param name="fileName">name of the file to read from</param>
        /// <param name="channel">channel number to load the waveform to</param>
        void LoadWaveformFromUSB(string fileName, int channel);

        /*
         * Saves the current waveform in the function generator's active memory to the USB storage device.
         * Waveform is saved in a new file with the name fileName.
         * The file is saved in the current working directory of the USB.
         * 
         * returns true if the file was successfully saved, and returns false if it was not
         * (i.e. a file with the same name already exists)
         * 
         * throws an ArgumentOutOfRangeException if the given channel does not exist on the function generator  
         * 
         *
         */
        /// <summary>
        /// Saves the current waveform in the given channel's active memory to the USB storage device.
        /// The waveform is saved in a new file with the name fileName. Does not overwrite files.
        /// </summary>
        /// <remarks>
        /// The file is saved in the current working directory of the USB.
        /// </remarks>
        /// <param name="fileName">filename to save waveform as</param>
        /// <param name="channel">The channel number to save the waveform from</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        /// <exception cref="ArgumentException">Thrown if a file with the given filename already exists</exception>
        /// <returns>true if the file was successfuly saved, and false if it was not</returns>
        bool SaveWaveformToUSB(string fileName, int channel);

        /*
         * Deletes the file with the name given in the parameter.
         * Must throw a FileNotFoundException if the file doesn't exist.
         * Throws an ArgumentException if the requested target is a directory.
         */
        /// <summary>
        ///  Deletes the file with the name given in the parameter.
        /// </summary>
        /// <exception cref="System.IO.FileNotFoundException">Thrown if the requested target doesn't exist</exception>
        /// <exception cref="ArgumentException">Thrown if the requested target is a directory</exception>
        /// <param name="fileName">The name of the file to delete</param>
        void DeleteFile(string fileName);

        /*
         * Deletes the directory with the name given in the parameter.
         * Must throw a DirectoryNotFoundException if the directory doesn't exist.
         * Throws an ArgumentException if the requested target is a file.
         */
        /// <summary>
        /// Deletes the directory with the name given in the parameter
        /// </summary>
        /// <exception cref="System.IO.DirectoryNotFoundException">Thrown if the requested target doesn't exist</exception>
        ///<exception cref="ArgumentException">Thrown if the requested target is a file</exception>
        /// <param name="directoryName">The name of the directory to delete</param>
        void DeleteDirectory(string directoryName);
    }
}