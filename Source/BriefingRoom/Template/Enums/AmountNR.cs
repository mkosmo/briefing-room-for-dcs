﻿/*
==========================================================================
This file is part of Briefing Room for DCS World, a mission
generator for DCS World, by @akaAgar (https://github.com/akaAgar/briefing-room-for-dcs)

Briefing Room for DCS World is free software: you can redistribute it
and/or modify it under the terms of the GNU General Public License
as published by the Free Software Foundation, either version 3 of
the License, or (at your option) any later version.

Briefing Room for DCS World is distributed in the hope that it will
be useful, but WITHOUT ANY WARRANTY; without even the implied warranty
of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Briefing Room for DCS World. If not, see https://www.gnu.org/licenses/
==========================================================================
*/

namespace BriefingRoom4DCS.Template
{
    /// <summary>
    /// Enumerates various relative amount values, from "None" to "Very high", with a "Random" value
    /// </summary>
    public enum AmountNR
    {
        /// <summary>
        /// Use a random value.
        /// </summary>
        Random,

        /// <summary>
        /// None at all.
        /// </summary>
        None,

        /// <summary>
        /// Very low amount.
        /// </summary>
        VeryLow,

        /// <summary>
        /// Low amount.
        /// </summary>
        Low,

        /// <summary>
        /// Average/balanced amount.
        /// </summary>
        Average,

        /// <summary>
        /// High amount.
        /// </summary>
        High,

        /// <summary>
        /// Very high amount.
        /// </summary>
        VeryHigh
    }
}
