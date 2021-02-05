/* ABRInput.cs
 *
 * Copyright (c) 2021 University of Minnesota
 * Authors: Bridger Herman <herma582@umn.edu>, Seth Johnson <sethalanjohnson@gmail.com>
 *
 */

namespace IVLab.ABREngine
{
    /// <summary>
    ///     Raw string values from a state JSON being passed to ABR
    ///
    ///     Matches `InputValue` definition from ABR State Schema
    ///
    ///     Parameters can have one or more inputs
    /// </summary>
    public class RawABRInput
    {
        /// <summary>
        ///     String representation of the C# type this ABR input is
        /// </summary>
        public string inputType;

        /// <summary>
        ///     The actual value of the input (string representation)
        /// </summary>
        public string inputValue;

        /// <summary>
        ///     The name of the parent parameter this input is associated with
        /// </summary>
        public string parameterName;

        /// <summary>
        ///     What type of input is it (variable, visasset, etc.)
        /// </summary>
        public string inputGenre;
    }

    /// <summary>
    ///     Interface that includes every input to a data impression. For every class
    ///     that implements this interface, there must be a constructor that takes a
    ///     single string argument!
    /// </summary>
    public interface IABRInput { }
}