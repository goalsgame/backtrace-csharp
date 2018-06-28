﻿using Backtrace.Interfaces;
using Backtrace.Model;
using Backtrace.Model.Database;
using Backtrace.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace Backtrace.Services
{
    /// <summary>
    /// Backtrace Database Context
    /// </summary>
    internal class BacktraceDatabaseContext : IBacktraceDatabaseContext
    {
        /// <summary>
        /// Database cache
        /// </summary>
        internal Dictionary<int, List<BacktraceDatabaseRecord>> BatchRetry = new Dictionary<int, List<BacktraceDatabaseRecord>>();

        /// <summary>
        /// Total database size on hard drive
        /// </summary>
        internal long TotalSize = 0;

        /// <summary>
        /// Total records in BacktraceDatabase
        /// </summary>
        internal int TotalRecords = 0;

        /// <summary>
        /// Path to database directory 
        /// </summary>
        private readonly string _path;

        /// <summary>
        /// Maximum number of retries
        /// </summary>
        private readonly int _retryNumber;

        /// <summary>
        /// Record order
        /// </summary>
        internal RetryOrder RetryOrder { get; set; }

        /// <summary>
        /// Initialize new instance of Backtrace Database Context
        /// </summary>
        /// <param name="path">Path to database directory</param>
        /// <param name="retryNumber">Total number of retries</param>
        /// <param name="retryOrder">Record order</param>
        public BacktraceDatabaseContext(string path, uint retryNumber, RetryOrder retryOrder)
        {
            _path = path;
            _retryNumber = checked((int)retryNumber);
            RetryOrder = retryOrder;
            SetupBatch();
        }

        /// <summary>
        /// Setup cache 
        /// </summary>
        private void SetupBatch()
        {
            if (_retryNumber == 0)
            {
                throw new ArgumentException($"{nameof(_retryNumber)} have to be greater than 0!");
            }
            for (int i = 0; i < _retryNumber; i++)
            {
                BatchRetry[i] = new List<BacktraceDatabaseRecord>();
            }
        }

        /// <summary>
        /// Add new record to database
        /// </summary>
        /// <param name="backtraceData">Diagnostic data that should be stored in database</param>
        /// <returns>New instance of DatabaseRecordy</returns>
        public virtual BacktraceDatabaseRecord Add(BacktraceData backtraceData)
        {
            if (backtraceData == null) throw new NullReferenceException(nameof(backtraceData));
            //create new record and save it on hard drive
            var record = new BacktraceDatabaseRecord<T>(backtraceData, _path);
            record.Save();
            //add record to database context
            return Add(record);
        }

        /// <summary>
        /// Add existing record to database
        /// </summary>
        /// <param name="backtraceRecord">Database record</param>
        public BacktraceDatabaseRecord<T> Add(BacktraceDatabaseRecord backtraceRecord)
        {
            if (backtraceRecord == null) throw new NullReferenceException(nameof(BacktraceDatabaseRecord));
            //lock record, because Add method returns record
            backtraceRecord.Locked = true;
            //increment total size of database
            TotalSize += backtraceRecord.Size;
            //add record to first batch
            BatchRetry[0].Add(backtraceRecord);
            //increment total records
            TotalRecords++;
            return backtraceRecord;
        }

        /// <summary>
        /// Check if any record exists
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public bool Any(BacktraceDatabaseRecord record)
        {
            return BatchRetry.SelectMany(n => n.Value).Any(n => n.Id == record.Id);
        }

        /// <summary>
        /// Check if any record exists
        /// </summary>
        public bool Any()
        {
            return TotalRecords != 0;
        }

        /// <summary>
        /// Delete existing record from database
        /// </summary>
        /// <param name="record">Database records to delete</param>
        public virtual void Delete(BacktraceDatabaseRecord record)
        {
            if (record == null)
            {
                return;
            }
            foreach (var key in BatchRetry.Keys)
            {
                foreach (var value in BatchRetry[key])
                {
                    if (value.Id == record.Id)
                    {
                        //delete value from hard drive
                        value.Delete();
                        //delete value from current batch
                        BatchRetry[key].Remove(value);
                        //decrement all records
                        TotalRecords--;
                        //decrement total size of database
                        TotalSize -= value.Size;
                        return;
                    }
                }
            }
            return;
        }

        /// <summary>
        /// Increment retry time for current record
        /// </summary>
        public void IncrementBatchRetry()
        {
            RemoveMaxRetries();
            IncrementBatches();
        }

        /// <summary>
        /// Increment each batch
        /// </summary>
        private void IncrementBatches()
        {
            for (int i = _retryNumber - 2; i >= 0; i--)
            {
                var temp = BatchRetry[i];
                BatchRetry[i] = new List<BacktraceDatabaseRecord>();
                BatchRetry[i + 1] = temp;
            }
        }

        /// <summary>
        /// Remove last batch
        /// </summary>
        private void RemoveMaxRetries()
        {
            var currentBatch = BatchRetry[_retryNumber - 1];
            var total = currentBatch.Count;
            for (int i = 0; i < total; i++)
            {
                var value = currentBatch[i];
                value.Delete();
                TotalRecords--;
                //decrement total size of database
                TotalSize -= value.Size;
            }
        }

        /// <summary>
        /// Get all database records
        /// </summary>
        /// <returns>all existing database records</returns>
        public IEnumerable<BacktraceDatabaseRecord> Get()
        {
            return BatchRetry.SelectMany(n => n.Value);
        }

        /// <summary>
        /// Get total number of records in database
        /// </summary>
        /// <returns></returns>
        public int Count() => TotalRecords;

        /// <summary>
        /// Dispose
        /// </summary>
        public virtual void Dispose()
        {
            TotalRecords = 0;
            BatchRetry.Clear();
        }

        /// <summary>
        /// Delete all records from database
        /// </summary>
        public void Clear()
        {
            var records = BatchRetry.SelectMany(n => n.Value);
            foreach (var record in records)
            {
                record.Delete();
            }
            TotalRecords = 0;
            TotalSize = 0;
            //clear all existing batches
            foreach (var batch in BatchRetry)
            {
                batch.Value.Clear();
            }
        }

        /// <summary>
        /// Get last exising database record. Method returns record based on order in Database
        /// </summary>
        /// <returns>First Backtrace database record</returns>
        public BacktraceDatabaseRecord LastOrDefault()
        {
            return RetryOrder == RetryOrder.Stack
                    ? GetLastRecord()
                    : GetFirstRecord();
        }

        /// <summary>
        /// Get first exising database record. Method returns record based on order in Database
        /// </summary>
        /// <returns>First Backtrace database record</returns>
        public BacktraceDatabaseRecord FirstOrDefault()
        {
            return RetryOrder == RetryOrder.Queue
                    ? GetFirstRecord()
                    : GetLastRecord();
        }

        /// <summary>
        /// Get first record in in-cache BacktraceDatabase
        /// </summary>
        /// <returns>First database record</returns>
        private BacktraceDatabaseRecord GetFirstRecord()
        {
            //get all batches (from the beginning)
            for (int i = 0; i < _retryNumber - 1; i++)
            {
                //if batch has any record that is not used
                //set lock to true 
                //and return file
                if (BatchRetry.ContainsKey(i) && BatchRetry[i].Any(n => !n.Locked))
                {
                    var record = BatchRetry[i].First(n => !n.Locked);
                    record.Locked = true;
                    return record;
                }
            }
            return null;
        }

        /// <summary>
        /// Get last record in in-cache BacktraceDatabase
        /// </summary>
        /// <returns>Last database record</returns>
        private BacktraceDatabaseRecord GetLastRecord()
        {
            for (int i = _retryNumber - 1; i >= 0; i--)
            {
                if (BatchRetry[i].Any(n => !n.Locked))
                {
                    var record = BatchRetry[i].Last(n => !n.Locked);
                    record.Locked = true;
                    return record;
                }
            }
            return null;
        }

        /// <summary>
        /// Get database size
        /// </summary>
        /// <returns>database size</returns>
        public long GetSize()
        {
            return TotalSize;
        }

        /// <summary>
        /// Get total number of records
        /// </summary>
        /// <returns>Total number of records</returns>
        public int GetTotalNumberOfRecords()
        {
            return TotalRecords;
        }
    }
}
