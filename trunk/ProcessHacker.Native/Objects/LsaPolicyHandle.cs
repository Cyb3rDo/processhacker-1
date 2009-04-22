﻿/*
 * Process Hacker - 
 *   local security policy handle
 * 
 * Copyright (C) 2008 wj32
 * 
 * This file is part of Process Hacker.
 * 
 * Process Hacker is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Process Hacker is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Process Hacker.  If not, see <http://www.gnu.org/licenses/>.
 */

using ProcessHacker.Native.Api;
using ProcessHacker.Native.Security;

namespace ProcessHacker.Native.Objects
{
    /// <summary>
    /// Represents a handle to the Windows service manager.
    /// </summary>
    public class LsaPolicyHandle : LsaHandle<PolicyAccess>
    {
        /// <summary>
        /// Connects to the local LSA policy.
        /// </summary>
        /// <param name="access">The desired access to the policy.</param>
        public LsaPolicyHandle(PolicyAccess access)
        {
            int status;
            ObjectAttributes attributes = new ObjectAttributes();
            int handle = 0;

            if ((status = Win32.LsaOpenPolicy(0, ref attributes, access, out handle)) < 0)
                Win32.ThrowLastError(status);

            this.Handle = handle;
        }
    }
}
