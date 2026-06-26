using System;
using System.Collections.Generic;
using NationalInstruments.NI4882;

namespace EsgSignalCreator.Instruments
{
    /// <summary>
    /// Message-based instrument transport backed by NI-488.2 (NationalInstruments.NI4882),
    /// addressing the instrument directly over GPIB by board and primary address.
    /// </summary>
    public sealed class Gpib488Instrument : IInstrument
    {
        private Device _device;
        private int _timeoutMs;

        public Gpib488Instrument(int boardNumber, byte primaryAddress, int timeoutMilliseconds = 5000)
        {
            BoardNumber = boardNumber;
            PrimaryAddress = primaryAddress;
            ResourceName = string.Format("GPIB{0}::{1}::INSTR", boardNumber, primaryAddress);

            _device = new Device(boardNumber, primaryAddress);
            TimeoutMilliseconds = timeoutMilliseconds;
        }

        public int BoardNumber { get; }

        public byte PrimaryAddress { get; }

        public string ResourceName { get; }

        public bool IsConnected => _device != null;

        public int TimeoutMilliseconds
        {
            get => _timeoutMs;
            set
            {
                _timeoutMs = value;
                if (_device != null) _device.IOTimeout = ToTimeoutValue(value);
            }
        }

        /// <summary>Scan a GPIB board for instruments that are present as listeners.</summary>
        public static IEnumerable<string> FindListeners(int boardNumber = 0)
        {
            var found = new List<string>();
            try
            {
                var board = new Board(boardNumber);
                AddressCollection listeners = board.FindListeners();
                foreach (Address address in listeners)
                    found.Add(string.Format("GPIB{0}::{1}::INSTR", boardNumber, address.PrimaryAddress));
            }
            catch (Exception)
            {
                // No board / no GPIB hardware present.
            }
            return found;
        }

        public void Write(string command)
        {
            EnsureOpen();
            _device.Write(command);
        }

        public string ReadString()
        {
            EnsureOpen();
            return _device.ReadString().TrimEnd('\r', '\n');
        }

        public string Query(string command)
        {
            Write(command);
            return ReadString();
        }

        public void WriteBinaryBlock(byte[] message)
        {
            EnsureOpen();
            // Device.Write(byte[]) sends the buffer and asserts EOI on the final byte.
            _device.Write(message);
        }

        private void EnsureOpen()
        {
            if (_device == null)
                throw new InvalidOperationException("The GPIB device is not open.");
        }

        public void Dispose()
        {
            // NI4882.Device has no Close/Dispose; releasing the reference is sufficient.
            _device = null;
        }

        private static TimeoutValue ToTimeoutValue(int milliseconds)
        {
            if (milliseconds <= 0) return TimeoutValue.None;
            if (milliseconds <= 30) return TimeoutValue.T30ms;
            if (milliseconds <= 100) return TimeoutValue.T100ms;
            if (milliseconds <= 300) return TimeoutValue.T300ms;
            if (milliseconds <= 1000) return TimeoutValue.T1s;
            if (milliseconds <= 3000) return TimeoutValue.T3s;
            if (milliseconds <= 10000) return TimeoutValue.T10s;
            if (milliseconds <= 30000) return TimeoutValue.T30s;
            if (milliseconds <= 100000) return TimeoutValue.T100s;
            return TimeoutValue.T300s;
        }
    }
}
