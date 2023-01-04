//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 

using System.IO;

namespace CodeCounter
{
    public class LicenseType
    {
        // What does this license mean ? https://opensource.org/licenses
        // Do nothing. (e.g. Public Domain)
        // I must attribute ? (MIT, Copyright X) (Permissive?) Where? in the distributed code or on my web site ?
        // I must make sources available. (LGPL)
        // I must publish my changes. 
        // I must open source all MY code ? (GPL) (CopyLeft)
        // Proprietary. Must have specific individual licensed permission to use.

    }

    public class LicenseRef
    {
        // define a license that we now obligate ourselves to by using this library.
        // What are we now obligated to do ? e.g. open source all our code? display a credit? make a copy of the sources available to all?
        // null = my personal code. (no license required)

        public string Name;     // Unique Name of the license. e.g. "GPL", "LGPL", "MIT", "Apache", "Public", etc.
        public string Url;      // URL that outlines the terms of the license.
        public string Text;     // This may just be text embedded in code. e.g. "Copyright XXX"

        public static bool IsLicenseRef(string line)
        {
            // Is this line of text a license reference ?
            // Id a comment at the head of the file.

            return false;
        }

    }

}
