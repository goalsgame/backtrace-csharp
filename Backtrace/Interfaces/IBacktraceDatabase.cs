﻿using Backtrace.Base;
using Backtrace.Types;
using System;
using System.Collections.Generic;
using System.Text;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Backtrace.Tests")]
namespace Backtrace.Interfaces
{
    /// <summary>
    /// Backtrace Database Interface
    /// Before start: Be sure that used directory is empty!
    /// </summary>
    public interface IBacktraceDatabase<T> : IDisposable
    {
        /// <summary>
        /// Create new minidump file in database directory path. Minidump file name is a random Guid
        /// </summary>
        /// <param name="backtraceReport">Current report</param>
        /// <param name="miniDumpType">Generated minidump type</param>
        /// <returns>Path to minidump file</returns>
        //string GenerateMiniDump(BacktraceReportBase<T> backtraceReport, MiniDumpType miniDumpType);

        /// <summary>
        /// Clear generated minidumps
        /// </summary>
        /// <param name="pathToMinidump">Path to created minidump</param>
        //void ClearMiniDump(string pathToMinidump);

        //---------------------------------------------------------------------------//
        /// <summary>
        /// Send all reports stored in BacktraceDatabase and clean database
        /// </summary>
        void Flush();

#if !NET35
        /// <summary>
        /// Send all reports stored in BacktraceDatabase asynchronous and clean database
        /// </summary>
        System.Threading.Tasks.Task FlushAsync();
#endif

        /// <summary>
        /// Remove all existing reports in BacktraceDatabase
        /// </summary>
        void Reset();

        /// <summary>
        /// Add new report to Database
        /// </summary>
        void Add();

        /// <summary>
        /// Delete report from Database
        /// </summary>
        void Delete();

        /// <summary>
        /// Get all repots stored in Database
        /// </summary>
        IEnumerable<BacktraceReportBase<T>> Get();

        /// <summary>
        /// Get all repots stored in Database
        /// </summary>
        //IEnumerable<BacktraceReportBase<T>> Get(Func<BacktraceReportBase<T>,IEnumerable<BacktraceReportBase<T>) filter);
    }
}
