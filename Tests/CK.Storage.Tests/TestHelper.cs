#region LGPL License
/*----------------------------------------------------------------------------
* This file (Tests\CK.Storage.Tests\TestHelper.cs) is part of CiviKey. 
*  
* CiviKey is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Lesser General Public License as published 
* by the Free Software Foundation, either version 3 of the License, or 
* (at your option) any later version. 
*  
* CiviKey is distributed in the hope that it will be useful, 
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the 
* GNU Lesser General Public License for more details. 
* You should have received a copy of the GNU Lesser General Public License 
* along with CiviKey.  If not, see <http://www.gnu.org/licenses/>. 
*  
* Copyright © 2007-2014, 
*     Invenietis <http://www.invenietis.com>,
*     In’Tech INFO <http://www.intechinfo.fr>,
* All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NUnit.Framework;
using CK.Core;

namespace Storage
{
    public static class TestHelper
    {
        static string _testFolder;
        static string _solutionFolder;

        static IActivityMonitor _monitor;
        static ActivityMonitorConsoleClient _console;

        static TestHelper()
        {
            _monitor = new ActivityMonitor();
            _console = _monitor.Output.RegisterClient( new ActivityMonitorConsoleClient() );
        }

        public static IActivityMonitor ConsoleMonitor
        {
            get { return _monitor; }
        }

        public static bool LogsToConsole
        {
            get { return _monitor.Output.Clients.Contains( _console ); }
            set
            {
                if( value ) _monitor.Output.RegisterUniqueClient( c => c ==_console, () => _console );
                else _monitor.Output.UnregisterClient( _console );
            }
        }

        public static string TestFolder
        {
            get
            {
                if( _testFolder == null ) InitalizePaths();
                return _testFolder;
            }
        }

        public static string SolutionFolder
        {
            get
            {
                if( _solutionFolder == null ) InitalizePaths();
                return _solutionFolder;
            }
        }

        public static void CleanupTestFolder()
        {
            int tryCount = 0;
            for( ; ; )
            {
                try
                {
                    if( Directory.Exists( TestFolder ) ) Directory.Delete( TestFolder, true );
                    Directory.CreateDirectory( TestFolder );
                    File.WriteAllText( Path.Combine( TestFolder, "TestWrite.txt" ), "Test write works." );
                    File.Delete( Path.Combine( TestFolder, "TestWrite.txt" ) );
                    return;
                }
                catch( Exception ex )
                {
                    if( ++tryCount == 20 ) throw;
                    ConsoleMonitor.Info().Send( ex, "While cleaning up test directory. Retrying." );
                    System.Threading.Thread.Sleep( 100 );
                }
            }
        }

        private static void InitalizePaths()
        {
            string p = new Uri( System.Reflection.Assembly.GetExecutingAssembly().CodeBase ).LocalPath;
            // => CK.XXX.Tests/bin/Debug/
            p = Path.GetDirectoryName( p );
            // => CK.XXX.Tests/bin/
            p = Path.GetDirectoryName( p );
            // => CK.XXX.Tests/
            p = Path.GetDirectoryName( p );
            // ==> CK.XXX.Tests/TestDir
            _testFolder = Path.Combine( p, "TestDir" );
            do
            {
                p = Path.GetDirectoryName( p );
            }
            while( !File.Exists( Path.Combine( p, "CK-Core.sln" ) ) );
            _solutionFolder = p;

            ConsoleMonitor.Info().Send( "SolutionFolder is: {1}\r\nTestFolder is: {0}", _testFolder, _solutionFolder );
            CleanupTestFolder();
        }

        static public string GetTestXmlFilePath( string prefix, string name )
        {
            return Path.Combine( TestFolder, prefix + "." + name + ".xml" );
        }

        static public void DumpFileToConsole( string path )
        {
            Console.WriteLine( File.ReadAllText( path ) );
        }

    }
}
