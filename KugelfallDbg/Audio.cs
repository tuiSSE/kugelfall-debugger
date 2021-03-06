﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
 
namespace KugelfallDbg
{
    //Das Gerät, von dem der Ton aufgezeichnet wird
    class Audio
    {
        public Audio(int _iDeviceNumber)
        {
            m_iDeviceNumber = _iDeviceNumber;
            m_iWaveInDevice = new NAudio.Wave.WaveIn();
            m_iWaveInDevice.WaveFormat = new NAudio.Wave.WaveFormat(SampleRate, m_iChannels);
            m_iWaveInDevice.DataAvailable += waveIn_DataAvailable;
            m_iWaveInDevice.DeviceNumber = m_iDeviceNumber;
            m_iWaveInDevice.BufferMilliseconds = 100;
            DateTimeMilli = 0;
            BufferMilliseconds = m_iWaveInDevice.BufferMilliseconds;
        }

        /**
         * void waveIn_DataAvailable(...)
         * Sobald Daten am Audioeingang vorliegen, wird dieses Event
         * aufgerufen und verarbeitet
         */

        long Duration = 0;
        bool m_bLock = false;
        void waveIn_DataAvailable(object sender, NAudio.Wave.WaveInEventArgs e)
        {
            if (m_bLock == false)
            {
                m_bLock = true;
                long SampleTime = Stoptimer.Time;  //Zeitstempel des aktuellen Samplepakets

                //System.IO.File.AppendAllText("audiopackets.txt", SampleTime.ToString() + " | ");

                float _maxsample = 0.0f;
                long SampleCount = 0;
                long SampleCounter = 0;

                //Samples auf Schwellenüberschreitung prüfen
                for (int index = 0; index < e.BytesRecorded; index += 2)
                {
                    short sample = (short)((e.Buffer[index + 1] << 8) | e.Buffer[index + 0]);

                    //Sample in 32-bit (4 Byte) umwandeln
                    float sample32 = sample / 32768f;

                    _maxsample = Math.Max(sample32, _maxsample);
                    SampleCounter++;

                    if (SampleCount == 0 && _maxsample >= m_fThreshold)   //Schwelle überschritten?
                    {
                        Duration = SampleTime - DateTimeMilli;
                        SampleCount = SampleCounter;
                        break;
                    }
                }

                OnNewMaxSample(this, _maxsample * 100);
                if (SampleCount != 0)
                {
                    OnThresholdExceed(this, Duration, SampleTime, SampleCount, (int)(e.BytesRecorded / 2));
                }

                DateTimeMilli = SampleTime;
                m_bLock = false;
            }
        }

        public long DateTimeMilli
        { get; private set; }

        private System.Diagnostics.Stopwatch m_BufferTimer = new System.Diagnostics.Stopwatch();

        public void StartRecording()
        {
            //Sollte der Recordingvorgang unterbrochen worden sein, muss das WaveInDevice neu angefordert werden
            if (m_iWaveInDevice == null)
            {
                m_iWaveInDevice = new NAudio.Wave.WaveIn();
                m_iWaveInDevice.DeviceNumber = m_iDeviceNumber;
                m_iWaveInDevice.WaveFormat = new NAudio.Wave.WaveFormat(m_iSampleRate,m_iChannels);
                m_iWaveInDevice.DataAvailable += waveIn_DataAvailable;
            }

            try
            {
                m_iWaveInDevice.StartRecording();
                m_bIsRecording = true;
            }
            catch (InvalidOperationException)    //WaveIn nimmt bereits auf
            {

            }
            catch (NAudio.MmException e)
            {
                //System.Windows.Forms.MessageBox.Show(e.Message);
            }
        }

        /**
         *  void StopRecording():
         *  Stoppt die Audioaufnahme 
         */
        public void StopRecording()
        {
            if (m_iWaveInDevice != null)
            {
                if (m_bIsRecording == true)
                {
                    try
                    {
                        m_iWaveInDevice.StopRecording();
                        m_iWaveInDevice.Dispose();
                        m_iWaveInDevice = null;
                    }
                    catch (NAudio.MmException)
                    {
                    
                    }

                    m_bIsRecording = false;
                }
            }
        }

        public NAudio.Wave.WaveIn WaveInDevice
        {
            get { return m_iWaveInDevice; }
        }

        public int Volume
        {
            get { return m_iVolume; }
            set { m_iVolume = value; }
        }

        public int SampleRate
        {
            get { return m_iSampleRate; }
            private set { m_iSampleRate = 16000; }
        }

        public float Threshold
        {
            get { return m_fThreshold;  }
            set { m_fThreshold = (float)((float)value/100.0f); }
        }

        private volatile float m_fThreshold = 0.75f;  ///Schwellenwert
        private int m_iSampleRate = 16000;              //Wieviele Samples pro Sekunde
        private int m_iChannels = 1;                    ///Wieviele Kanäle sollen zur Aufnahme benutzt werden (Default: 1 -> Mono)
        private int m_iDeviceNumber;                    ///Nummer des Soundaufnahmegerätes (Dient zur Identifikation)
        private volatile int m_iVolume;                 ///Die aktuelle Lautstärke
        private NAudio.Wave.WaveIn m_iWaveInDevice;
        private bool m_bIsRecording = false;
        
        //Eigene Eventhandler, welche zur Überwachung des Audiosignals dienen
        
        //Handler, der immer das aktuellste, lauteste Sample herausgibt
        public delegate void MaxSampleHandler(object sender, float MaxSample);
        public event MaxSampleHandler NewMaxSample;
        protected void OnNewMaxSample(object sender, float MaxSample)
        {
            if (NewMaxSample != null)   //Prüfung, ob sich sich jemand in das Event eingeschrieben hat
            {
                NewMaxSample(this, MaxSample);  //Das aktuelle MaxSample weitergeben
            }
        }

        public int BufferMilliseconds { get; private set; }

        //ThresholdExceedHandler: Eventhandler bei Überschreitung des Schwellenwertes
        public delegate void ThresholdExceedHandler(object sender, long _Duration, long _EndTime, long _RaisedSample, int _Samples);

        public event ThresholdExceedHandler ThresholdExceeded;
        protected void OnThresholdExceed(object sender, long _Duration, long _EndTime, long _RaisedSample, int _Samples)
        {
            if (ThresholdExceeded != null)
            {
                ThresholdExceeded(this, _Duration, _EndTime, _RaisedSample, _Samples);
            }
        }
    }
}
