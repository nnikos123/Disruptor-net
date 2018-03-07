﻿using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class EventPollerTest
    {
        [Test]
        public void ShouldPollForEvents()
        {
            var gatingSequence = new Sequence();
            var sequencer = new SingleProducerSequencer(16, new BusySpinWaitStrategy());

            bool Handler(object e, long s, bool b) => false;

            var data = new object[16];
            var provider = new DataProvider(data);

            var poller = sequencer.NewPoller(provider, gatingSequence);
            var evt = new object();
            data[0] = evt;

            Assert.That(poller.Poll(Handler), Is.EqualTo(PollState.Idle));

            // Publish Event.
            sequencer.Publish(sequencer.Next());
            Assert.That(poller.Poll(Handler), Is.EqualTo(PollState.Gating));

            gatingSequence.IncrementAndGet();

            Assert.That(poller.Poll(Handler), Is.EqualTo(PollState.Processing));
        }

        [Test]
        public void ShouldSuccessfullyPollWhenBufferIsFull()
        {
            var events = new List<byte[]>();

            byte[] Factory() => new byte[1];

            bool Handler(byte[] data, long sequence, bool endOfBatch)
            {
                events.Add(data);
                return !endOfBatch;
            }

            var ringBuffer = RingBuffer<byte[]>.CreateMultiProducer(Factory, 4, new SleepingWaitStrategy());

            var poller = ringBuffer.NewPoller();
            ringBuffer.AddGatingSequences(poller.Sequence);

            const int count = 4;

            for (byte i = 1; i <= count; ++i)
            {
                var next = ringBuffer.Next();
                ringBuffer[next][0] = i;
                ringBuffer.Publish(next);
            }

            // think of another thread
            poller.Poll(Handler);

            Assert.That(events.Count, Is.EqualTo(4));
        }

        private class DataProvider : IDataProvider<object>
        {
            private readonly object[] _data;

            public DataProvider(object[] data)
            {
                _data = data;
            }

            public object this[long sequence] => _data[sequence];
        }
    }
}
