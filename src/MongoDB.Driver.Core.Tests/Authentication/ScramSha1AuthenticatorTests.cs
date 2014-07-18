﻿/* Copyright 2013-2014 MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Threading;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver.Core.Authentication;
using MongoDB.Driver.Core.Exceptions;
using MongoDB.Driver.Core.Tests.Helpers;
using MongoDB.Driver.Core.Tests.Misc;
using MongoDB.Driver.Core.WireProtocol.Messages;
using NUnit.Framework;

namespace MongoDB.Driver.Core.Tests.Authentication
{
    [TestFixture]
    public class ScramSha1AuthenticatorTests
    {
        private static readonly UsernamePasswordCredential __credential = new UsernamePasswordCredential("source", "user", "pencil");

        [Test]
        public void Constructor_should_throw_an_ArgumentNullException_when_credential_is_null()
        {
            Action act = () => new ScramSha1Authenticator(null);

            act.ShouldThrow<ArgumentNullException>();
        }

        [Test]
        public void AuthenticateAsync_should_throw_an_AuthenticationException_when_authentication_fails()
        {
            var subject = new ScramSha1Authenticator(__credential);

            var reply = MessageHelper.BuildNoDocumentsReturnedReply<RawBsonDocument>();
            var connection = new MockRootConnection();
            connection.EnqueueReplyMessage(reply);

            Action act = () => subject.AuthenticateAsync(connection, Timeout.InfiniteTimeSpan, CancellationToken.None).Wait();

            act.ShouldThrow<AuthenticationException>();
        }

        [Test]
        public void AuthenticateAsync_should_not_throw_when_authentication_succeeds()
        {
            var randomStringGenerator = new ConstantRandomStringGenerator("fyko+d2lbbFgONRv9qkxdawL");
            var subject = new ScramSha1Authenticator(__credential, randomStringGenerator);

            var saslStartReply = MessageHelper.BuildSuccessReply<RawBsonDocument>(
                RawBsonDocumentHelper.FromJson("{conversationId: 1, payload: BinData(0,\"cj1meWtvK2QybGJiRmdPTlJ2OXFreGRhd0xIbytWZ2s3cXZVT0tVd3VXTElXZzRsLzlTcmFHTUhFRSxzPXJROVpZM01udEJldVAzRTFURFZDNHc9PSxpPTEwMDAw\"), done: false, ok: 1}"));
            var saslContinueReply = MessageHelper.BuildSuccessReply<RawBsonDocument>(
                RawBsonDocumentHelper.FromJson("{conversationId: 1, payload: BinData(0,\"dj1VTVdlSTI1SkQxeU5ZWlJNcFo0Vkh2aFo5ZTA9\"), done: true, ok: 1}"));

            var connection = new MockRootConnection();
            connection.EnqueueReplyMessage(saslStartReply);
            connection.EnqueueReplyMessage(saslContinueReply);

            var currentRequestId = RequestMessage.CurrentGlobalRequestId;
            Action act = () => subject.AuthenticateAsync(connection, Timeout.InfiniteTimeSpan, CancellationToken.None).Wait();
            act.ShouldNotThrow();

            var sentMessages = MessageHelper.TranslateRequestsToBsonDocuments(connection.GetSentMessages());
            sentMessages.Count.Should().Be(2);

            sentMessages[0].Should().Be("{opcode: \"query\", requestId: " + (currentRequestId + 1) + ", database: \"source\", collection: \"$cmd\", batchSize: -1, slaveOk: true, query: {saslStart: 1, mechanism: \"SCRAM-SHA-1\", payload: new BinData(0, \"biwsbj11c2VyLHI9ZnlrbytkMmxiYkZnT05Sdjlxa3hkYXdM\")}}");
            sentMessages[1].Should().Be("{opcode: \"query\", requestId: " + (currentRequestId + 2) + ", database: \"source\", collection: \"$cmd\", batchSize: -1, slaveOk: true, query: {saslContinue: 1, conversationId: 1, payload: new BinData(0, \"Yz1iaXdzLHI9ZnlrbytkMmxiYkZnT05Sdjlxa3hkYXdMSG8rVmdrN3F2VU9LVXd1V0xJV2c0bC85U3JhR01IRUUscD1NQzJUOEJ2Ym1XUmNrRHc4b1dsNUlWZ2h3Q1k9\")}}");
        }
    }
}