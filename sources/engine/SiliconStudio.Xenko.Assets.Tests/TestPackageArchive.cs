﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.

using System;
using System.IO;
using NUnit.Framework;
using SiliconStudio.Assets;
using SiliconStudio.Core.Diagnostics;
using SiliconStudio.Xenko.Assets.Tasks;

namespace SiliconStudio.Xenko.Assets.Tests
{
    [TestFixture]
    public class TestPackageArchive
    {

        [Test, Ignore("Need to check why it was disabled")]
        public void TestBasicPackageCreateSaveLoad()
        {
            // Override search path since we are in a unit test directory
            DirectoryHelper.PackageDirectoryOverride = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..");

            var defaultPackage = PackageStore.Instance.DefaultPackage;

            PackageArchive.Build(GlobalLogger.GetLogger("PackageArchiveTest"), defaultPackage);
        }
    }
}