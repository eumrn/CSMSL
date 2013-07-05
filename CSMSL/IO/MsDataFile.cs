﻿using System;
using System.Collections.Generic;
using System.IO;
using CSMSL.Spectral;
using CSMSL.Proteomics;

namespace CSMSL.IO
{
    public abstract class MSDataFile : IDisposable, IEquatable<MSDataFile>, IEnumerable<MSDataScan>
    {
        internal MSDataScan[] Scans = null;

        private string _filePath;

        private int _firstSpectrumNumber = -1;

        private bool _isOpen;

        private int _lastSpectrumNumber = -1;

        private string _name;

        protected MSDataFile(string filePath, MSDataFileType filetype = MSDataFileType.UnKnown, bool openImmediately = false)
        {
            if (!File.Exists(filePath) && !Directory.Exists(filePath))
            {
                throw new IOException(string.Format("The MS data file {0} does not currently exist", filePath));
            }
            FilePath = filePath;
            FileType = filetype;
            _isOpen = false;
            if (openImmediately) Open();
        }



        public string FilePath
        {
            get { return _filePath; }
            private set
            {
                _filePath = value;
                _name = Path.GetFileNameWithoutExtension(value);
            }
        }

        public MSDataFileType FileType { get; private set; }

        public virtual int FirstSpectrumNumber
        {
            get
            {
                if (_firstSpectrumNumber < 0)
                {
                    _firstSpectrumNumber = GetFirstSpectrumNumber();
                }
                return _firstSpectrumNumber;
            }
            set
            {
                _firstSpectrumNumber = value;
            }
        }

        public bool IsOpen
        {
            get { return _isOpen; }
            protected set { _isOpen = value; }
        }

        public virtual int LastSpectrumNumber
        {
            get
            {
                if (_lastSpectrumNumber < 0)
                {
                    _lastSpectrumNumber = GetLastSpectrumNumber();
                }
                return _lastSpectrumNumber;
            }
        }

      

        public string Name
        {
            get { return _name; }
        }

        public MSDataScan this[int spectrumNumber]
        {
            get
            {
                return GetMsScan(spectrumNumber);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public virtual void Dispose()
        {
            if (Scans != null)
            {               
                ClearCachedScans();
                Scans = null;                
            }
            _isOpen = false;
        }

        public bool Equals(MSDataFile other)
        {
            if (ReferenceEquals(this, other)) return true;
            return FilePath.Equals(other.FilePath);
        }

        public IEnumerator<MSDataScan> GetEnumerator()
        {
            return GetMsScans().GetEnumerator();
        }

        public override int GetHashCode()
        {
            return FilePath.GetHashCode();
        }

        public abstract DissociationType GetDissociationType(int spectrumNumber, int msnOrder = 2);

        public abstract int GetMsnOrder(int spectrumNumber);

        /// <summary>
        /// Get the MS Scan at the specific spectrum number.
        /// </summary>
        /// <param name="spectrumNumber">The spectrum number to get the MS Scan at</param>      
        /// <returns></returns>
        public virtual MSDataScan GetMsScan(int spectrumNumber)
        {
            if (Scans == null)
            {
                Scans = new MSDataScan[LastSpectrumNumber + 1];
            }
           
            if (Scans[spectrumNumber] == null)
            {
                return Scans[spectrumNumber] = GetMSDataScan(spectrumNumber);                
            }

            return Scans[spectrumNumber];
        }

        public virtual void ClearCachedScans()
        {
            if (Scans == null)
                return;
            Array.Clear(Scans, 0, Scans.Length);
        }

        protected virtual MSDataScan GetMSDataScan(int spectrumNumber)
        {           
            MSDataScan scan;
            int msn = GetMsnOrder(spectrumNumber);
            if (msn > 1)
            {
                MsnDataScan msnscan = new MsnDataScan(spectrumNumber, msn, this);
               // msnscan.PrecursorMz = GetPrecusorMz(spectrumNumber, msn);               
               // msnscan.IsolationRange = GetIsolationRange(spectrumNumber, msn);
                //msnscan.DissociationType = GetDissociationType(spectrumNumber, msn);
                //msnscan.PrecursorCharge = GetPrecusorCharge(spectrumNumber, msn);
                scan = msnscan;
            }
            else
            {
                scan = new MSDataScan(spectrumNumber, msn, this);
            }
            //scan.MassSpectrum = GetMzSpectrum(spectrumNumber);
            //scan.Resolution = GetResolution(spectrumNumber);
            //scan.InjectionTime = GetInjectionTime(spectrumNumber);
            //scan.RetentionTime = GetRetentionTime(spectrumNumber);
            //scan.Polarity = GetPolarity(spectrumNumber);
            //scan.MzAnalyzer = GetMzAnalyzer(spectrumNumber);
            //scan.MzRange = GetMzRange(spectrumNumber);

            return scan;            
        }

        public abstract short GetPrecusorCharge(int spectrumNumber, int msnOrder = 2);

        public abstract MassRange GetMzRange(int spectrumNumber);

        public IEnumerable<MSDataScan> GetMsScans()
        {
            return GetMsScans(FirstSpectrumNumber, LastSpectrumNumber);
        }

        public abstract double GetPrecusorMz(int spectrumNumber, int msnOrder = 2);

        public abstract double GetIsolationWidth(int spectrumNumber, int msnOrder = 2);

        public virtual MassRange GetIsolationRange(int spectrumNumber, int msnOrder = 2)
        {
            double precursormz = GetPrecusorMz(spectrumNumber, msnOrder);
            double half_width = GetIsolationWidth(spectrumNumber, msnOrder) / 2;
            return new MassRange(precursormz - half_width, precursormz + half_width);
        }

        public IEnumerable<MSDataScan> GetMsScans(int firstSpectrumNumber, int lastSpectrumNumber)
        {
            for (int spectrumNumber = firstSpectrumNumber; spectrumNumber <= lastSpectrumNumber; spectrumNumber++)
            {
                yield return GetMsScan(spectrumNumber);
            }
        }

        public IEnumerable<MSDataScan> GetMsScans(IRange<int> range)
        {
            return GetMsScans(range.Minimum, range.Maximum);
        }

        public abstract MZAnalyzerType GetMzAnalyzer(int spectrumNumber);

        public abstract MassSpectrum GetMzSpectrum(int spectrumNumber);

        public abstract Polarity GetPolarity(int spectrumNumber);

        public abstract double GetRetentionTime(int spectrumNumber);

        public abstract double GetInjectionTime(int spectrumNumber);

        public abstract double GetResolution(int spectrumNumber);

        /// <summary>
        /// Open up a connection to the underlying MS data stream
        /// </summary>
        public virtual void Open()
        {
            _isOpen = true;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", Name, Enum.GetName(typeof(MSDataFileType), FileType));
        }

        protected abstract int GetFirstSpectrumNumber();

        protected abstract int GetLastSpectrumNumber();

        public abstract int GetSpectrumNumber(double retentionTime);     
    }
}