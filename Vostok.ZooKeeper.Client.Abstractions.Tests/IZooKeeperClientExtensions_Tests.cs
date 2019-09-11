﻿using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Client.Abstractions.Tests
{
    [TestFixture]
    public class IZooKeeperClientExtensions_Tests
    {
        private IZooKeeperClient zooKeeperClient;
        private Func<byte[], byte[]> dummyUpdate;

        [SetUp]
        public void SetUp()
        {
            zooKeeperClient = Substitute.For<IZooKeeperClient>();
            dummyUpdate = bytes => bytes;
        }

        [Test]
        public void TryUpdateDataAsync_should_return_true_if_get_update_set_data_are_successful()
        {
            const string path = "zk/default";
            var bytes = new byte[] {0, 1, 1, 2, 3, 5, 8, 13};
            zooKeeperClient.GetDataAsync(Arg.Any<GetDataRequest>()).Returns(GetDataResult.Successful(path, bytes, Stat));

            zooKeeperClient.SetDataAsync(Arg.Any<SetDataRequest>()).Returns(SetDataResult.Successful(path, Stat));

            zooKeeperClient.TryUpdateDataAsync(path, dummyUpdate)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeTrue();
        }

        [Test]
        public void TryUpdateDataAsync_should_return_false_without_retries_if_cant_read_data()
        {
            const string path = "zk/default";
            zooKeeperClient.GetDataAsync(Arg.Any<GetDataRequest>()).Returns(UnknownErrorGetData(path));

            zooKeeperClient.TryUpdateDataAsync(path, dummyUpdate)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeFalse();
        }

        [Test]
        public void TryUpdateDataAsync_should_make_all_attempts_to_set_data_and_return_false()
        {
            const string path = "zk/default";
            const int attempts = 10;
            var bytes = new byte[] {0, 1, 1, 2, 3, 5, 8, 13};
            zooKeeperClient.GetDataAsync(Arg.Any<GetDataRequest>())
                .Returns(GetDataResult.Successful(path, bytes, Stat));

            zooKeeperClient.SetDataAsync(Arg.Any<SetDataRequest>())
                .Returns(SetDataResult.Unsuccessful(ZooKeeperStatus.VersionsMismatch, path, null));

            zooKeeperClient.TryUpdateDataAsync(path, dummyUpdate, attempts)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeFalse();

            zooKeeperClient.ReceivedWithAnyArgs(attempts)
                .SetDataAsync(Arg.Any<SetDataRequest>());
        }

        [Test]
        public void TryUpdateDataAsync_should_apply_update_func_to_bytes()
        {
            const string path = "zk/default";
            const int attempts = 10;
            var bytes = new byte[] {0, 1, 1, 2, 3, 5, 8, 13};
            var updatedBytes = new byte[] {3, 1, 4, 2};

            byte[] UpdateFunc(byte[] oldBytes) =>
                updatedBytes;

            zooKeeperClient.GetDataAsync(Arg.Any<GetDataRequest>())
                .Returns(GetDataResult.Successful(path, bytes, Stat));

            zooKeeperClient.SetDataAsync(Arg.Any<SetDataRequest>())
                .Returns(SetDataResult.Unsuccessful(ZooKeeperStatus.VersionsMismatch, path, null));

            zooKeeperClient.TryUpdateDataAsync(path, UpdateFunc, attempts)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeFalse();

            zooKeeperClient.Received(attempts)
                .SetDataAsync(Arg.Is<SetDataRequest>(sendReq => sendReq.Data.Equals(updatedBytes)));
        }

        [TestCaseSource(nameof(TryUpdate_FailFast_OnTheseErrors))]
        public void TryUpdateDataAsync_should_return_false_after_one_attempt_if_set_data_fails(ZooKeeperStatus failFastStatus)
        {
            const string path = "zk/default";
            var bytes = new byte[] {0, 1, 1, 2, 3, 5, 8, 13};

            zooKeeperClient.GetDataAsync(Arg.Any<GetDataRequest>())
                .Returns(GetDataResult.Successful(path, bytes, Stat));

            zooKeeperClient.SetDataAsync(Arg.Any<SetDataRequest>())
                .Returns(SetDataResult.Unsuccessful(failFastStatus, path, null));

            zooKeeperClient.TryUpdateDataAsync(path, dummyUpdate)
                .GetAwaiter()
                .GetResult()
                .Should()
                .BeFalse();

            zooKeeperClient.ReceivedWithAnyArgs(1).SetDataAsync(Arg.Any<SetDataRequest>());
        }

        public static IEnumerable<ZooKeeperStatus> TryUpdate_FailFast_OnTheseErrors()
        {
            return new[]
            {
                ZooKeeperStatus.BadArguments,
                ZooKeeperStatus.ChildrenForEphemeralAreNotAllowed,
                ZooKeeperStatus.ConnectionLoss,
                ZooKeeperStatus.NodeHasChildren,
                ZooKeeperStatus.NodeAlreadyExists,
                ZooKeeperStatus.NodeNotFound,
                ZooKeeperStatus.NotConnected,
                ZooKeeperStatus.NotReadonlyOperation,
                ZooKeeperStatus.SessionExpired,
                ZooKeeperStatus.SessionMoved,
                ZooKeeperStatus.Timeout,
                ZooKeeperStatus.UnknownError
            };
        }

        private static GetDataResult UnknownErrorGetData(string path) =>
            GetDataResult.Unsuccessful(ZooKeeperStatus.UnknownError, path, null);

        private static NodeStat Stat
        {
            get => new NodeStat(1, 2, 3, 4, 5, 6, 1, 1, 1, 1, 0);
        }
    }
}