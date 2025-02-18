﻿using System;
using System.Collections.Generic;
using System.Text;
using BeetleX.Buffers;
using System.Net;
using MailKit.Security;

namespace Infinispan.Hotrod.Core.Commands
{
    public class AUTH : Command
    {
        public AUTH(string mech, NetworkCredential c)
        {
            Credential = c;
            NetworkReceive = OnReceive;
            SaslMechName = mech;
            c.Domain="hotrod/node0";
            SaslMech = SaslMechanism.Create(mech, new Uri("hotrod://node0"), c);
        }
        public int TimeOut { get; set; }

        public override string Name => "AUTH";
        public override Byte Code => 0x23;
        public NetworkCredential Credential;
        public SaslMechanism SaslMech;
        public string SaslMechName;
        public byte Completed;
        public byte[] Challenge= new byte[0];
        private int Step = 0;
        public override void OnExecute(CommandContext ctx)
        {
            base.OnExecute(ctx);
        }

        public override void Execute(CommandContext ctx, InfinispanClient client, PipeStream stream)
        {
            switch (this.SaslMechName) {
                case "DIGEST-MD5":
                    executeDigestMd5(ctx, client, stream);
                break;
                case "PLAIN":
                    executePlain(ctx, client, stream);
                break;
                default:
                    if (!this.SaslMechName.StartsWith("SCRAM-SHA-")) {
                        Completed=1;
                        break;
                    }
                    executeScram(ctx, client, stream);
                break;
            }
        }
        private void executeDigestMd5(CommandContext ctx, InfinispanClient client, PipeStream stream) {
            switch (Step) {
                case 0:
                    base.Execute(ctx, client, stream);
                    Codec.writeArray(Encoding.ASCII.GetBytes(SaslMechName), stream);
                    Codec.writeArray(Challenge, stream);
                    stream.Flush();
                    Step++;
                    break;
                case 1:
                    base.Execute(ctx, client, stream);
                    Codec.writeArray(Encoding.ASCII.GetBytes(SaslMechName), stream);
                    var s = Convert.ToBase64String(Challenge);
                    s = SaslMech.Challenge(s);
                    Challenge = Convert.FromBase64String(s);
                    Codec.writeArray(Challenge, stream);
                    Completed=1;
                    stream.Flush();
                    Step++;
                    break;
            }            
        }
        private void executePlain(CommandContext ctx, InfinispanClient client, PipeStream stream) {
            switch (Step) {
                case 0:
                    Step++;
                    goto case 1;
                case 1:
                    base.Execute(ctx, client, stream);
                    Codec.writeArray(Encoding.ASCII.GetBytes(SaslMechName), stream);
                    var s = Convert.ToBase64String(Challenge);
                    s = SaslMech.Challenge(s);
                    Challenge = Convert.FromBase64String(s);
                    Codec.writeArray(Challenge, stream);
                    Completed=1;
                    stream.Flush();
                    Step++;
                    break;
                default:
                    Completed=1;
                    break;
            }
        }
        private void executeScram(CommandContext ctx, InfinispanClient client, PipeStream stream) {
            switch (Step) {
                case 0:
                    base.Execute(ctx, client, stream);
                    Codec.writeArray(Encoding.ASCII.GetBytes(SaslMechName), stream);
                    var s1 = Convert.ToBase64String(Challenge);
                    s1 = SaslMech.Challenge(s1);
                    Challenge = Convert.FromBase64String(s1);
                    Codec.writeArray(Challenge, stream);
                    stream.Flush();
                    Step++;
                break;
                case 1:
                    base.Execute(ctx, client, stream);
                    Codec.writeArray(Encoding.ASCII.GetBytes(SaslMechName), stream);
                    var s = Convert.ToBase64String(Challenge);
                    s = SaslMech.Challenge(s);
                    Challenge = Convert.FromBase64String(s);
                    Codec.writeArray(Challenge, stream);
                    Completed=1;
                    stream.Flush();
                    Step++;
                    break;
                default:
                    Completed=1;
                    break;
            }
        }
        public override Result OnReceive(InfinispanRequest request, PipeStream stream)
        {
            var completed = (byte)stream.ReadByte();
            Challenge = Codec.readArray(stream);
            return new Result{ Status =  ResultStatus.Completed, ResultType = ResultType.Object };
        }
    }
}
