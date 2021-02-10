using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UdtSharp;

namespace p2pcopy
{
    internal class Program
    {
        private static readonly List<Socket> Sockets = new List<Socket>();
        private static readonly List<int> Seconds = new List<int> { 10, 20, 30, 40, 50, 60 };

        static async Task Main(string[] args)
        {
            CommandLineArguments cla = CommandLineArguments.Parse(args);

            if (cla == null || (!cla.Sender && !cla.Receiver))
            {
                CommandLineArguments.ShowUsage();
                Environment.Exit(1);
                return;
            }
            else if (cla.Sender && String.IsNullOrEmpty(cla.File))
            {
                CommandLineArguments.ShowUsage();
                Environment.Exit(1);
                return;
            }

            if (cla.File != null && (!(File.Exists(cla.File))))
            {
                Console.Error.WriteLine($"File {cla.File} does not exist");
                Environment.Exit(2);
                return;
            }

            try
            {
                if (cla.LocalPort != -1)
                {
                    Console.WriteLine("Using local port: {0}", cla.LocalPort);
                }
                else
                {
                    cla.LocalPort = 0;
                }

                await InitializeSocketsAsync();

                Console.WriteLine();

                Console.Write("Enter the ip of your peer: ");
                var remoteIp = Console.ReadLine();

                if (string.IsNullOrEmpty(remoteIp))
                {
                    Console.WriteLine("Invalid ip entered");
                    return;
                }

                Console.Write("Enter the ports of your peer (comma separate): ");
                var peerPorts = Console.ReadLine();

                if (string.IsNullOrEmpty(peerPorts))
                {
                    Console.WriteLine("Invalid ports entered");
                    return;
                }

                var remotePorts = ParseRemotePort(peerPorts);

                UdtSocket connection = await PeerConnectAsync(remoteIp, remotePorts);

                if (connection == null)
                {
                    Console.Error.WriteLine("Failed to establish P2P conn to {0}", remoteIp);
                    Environment.Exit(3);
                    return;
                }

                try
                {
                    if (cla.Sender)
                    {
                        Sender.Run(connection, cla.File, cla.Verbose);
                        return;
                    }

                    Receiver.Run(connection);
                }
                finally
                {
                    connection.Close();
                }
            }
            finally
            {
                foreach (var socket in Sockets)
                    socket?.Close();
            }
        }

        private class CommandLineArguments
        {
            internal bool Sender = false;

            internal bool Receiver = false;

            internal string File;

            internal int LocalPort = -1;

            internal bool Verbose = false;

            static internal CommandLineArguments Parse(string[] args)
            {
                CommandLineArguments result = new CommandLineArguments();

                int i = 0;

                while (i < args.Length)
                {
                    string arg = args[i++];

                    switch (arg)
                    {
                        case "sender":
                            result.Sender = true;
                            break;

                        case "receiver":
                            result.Receiver = true;
                            break;

                        case "--verbose":
                            result.Verbose = true;
                            break;

                        case "--file":
                            if (args.Length == i) return null;
                            result.File = args[i++];
                            break;

                        case "--localport":
                            if (args.Length == i) return null;
                            result.LocalPort = int.Parse(args[i++]);
                            break;

                        case "help":
                            return null;
                    }
                }

                return result;
            }

            static internal void ShowUsage()
            {
                Console.WriteLine("p2pcopy [receiver|sender --file file_to_send]");
            }
        }

        static int[] ParseRemotePort(string portsStr)
        {
            var split = portsStr.Split(',');
            var ports = new int[split.Length];

            for (var i = 0; i < split.Length; i++)
                ports[i] = int.Parse(split[i]);

            return ports;
        }

        class P2pEndPoint
        {
            internal IPEndPoint External;
            internal IPEndPoint Internal;
        }

        static async Task<P2pEndPoint> GetExternalEndPointAsync(Socket socket, bool printMessages = true)
        {
            // https://gist.github.com/zziuni/3741933

            List<Tuple<string, int>> stunServers = new List<Tuple<string, int>>();
            stunServers.Add(new Tuple<string, int>("stun.l.google.com", 19302));
            stunServers.Add(new Tuple<string, int>("iphone-stun.strato-iphone.de", 3478));
            stunServers.Add(new Tuple<string, int>("numb.viagenie.ca", 3478));
            stunServers.Add(new Tuple<string, int>("s1.taraba.net", 3478));
            stunServers.Add(new Tuple<string, int>("s2.taraba.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.12connect.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.12voip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.1und1.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.2talk.co.nz", 3478));
            stunServers.Add(new Tuple<string, int>("stun.2talk.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.3clogic.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.3cx.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.a-mm.tv", 3478));
            stunServers.Add(new Tuple<string, int>("stun.aa.net.uk", 3478));
            stunServers.Add(new Tuple<string, int>("stun.acrobits.cz", 3478));
            stunServers.Add(new Tuple<string, int>("stun.actionvoip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.advfn.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.aeta-audio.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.aeta.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.alltel.com.au", 3478));
            stunServers.Add(new Tuple<string, int>("stun.altar.com.pl", 3478));
            stunServers.Add(new Tuple<string, int>("stun.annatel.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.antisip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.arbuz.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.avigora.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.avigora.fr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.awa-shima.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.awt.be", 3478));
            stunServers.Add(new Tuple<string, int>("stun.b2b2c.ca", 3478));
            stunServers.Add(new Tuple<string, int>("stun.bahnhof.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.barracuda.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.bluesip.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.bmwgs.cz", 3478));
            stunServers.Add(new Tuple<string, int>("stun.botonakis.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.budgetphone.nl", 3478));
            stunServers.Add(new Tuple<string, int>("stun.budgetsip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.cablenet-as.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.callromania.ro", 3478));
            stunServers.Add(new Tuple<string, int>("stun.callwithus.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.cbsys.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.chathelp.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.cheapvoip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ciktel.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.cloopen.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.colouredlines.com.au", 3478));
            stunServers.Add(new Tuple<string, int>("stun.comfi.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.commpeak.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.comtube.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.comtube.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.cope.es", 3478));
            stunServers.Add(new Tuple<string, int>("stun.counterpath.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.counterpath.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.cryptonit.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.darioflaccovio.it", 3478));
            stunServers.Add(new Tuple<string, int>("stun.datamanagement.it", 3478));
            stunServers.Add(new Tuple<string, int>("stun.dcalling.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.decanet.fr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.demos.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.develz.org", 3478));
            stunServers.Add(new Tuple<string, int>("stun.dingaling.ca", 3478));
            stunServers.Add(new Tuple<string, int>("stun.doublerobotics.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.drogon.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.duocom.es", 3478));
            stunServers.Add(new Tuple<string, int>("stun.dus.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.e-fon.ch", 3478));
            stunServers.Add(new Tuple<string, int>("stun.easybell.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.easycall.pl", 3478));
            stunServers.Add(new Tuple<string, int>("stun.easyvoip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.efficace-factory.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.einsundeins.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.einsundeins.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ekiga.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.epygi.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.etoilediese.fr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.eyeball.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.faktortel.com.au", 3478));
            stunServers.Add(new Tuple<string, int>("stun.freecall.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.freeswitch.org", 3478));
            stunServers.Add(new Tuple<string, int>("stun.freevoipdeal.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.fuzemeeting.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.gmx.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.gmx.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.gradwell.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.halonet.pl", 3478));
            stunServers.Add(new Tuple<string, int>("stun.hellonanu.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.hoiio.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.hosteurope.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ideasip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.imesh.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.infra.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.internetcalls.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.intervoip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ipcomms.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ipfire.org", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ippi.fr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ipshka.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.iptel.org", 3478));
            stunServers.Add(new Tuple<string, int>("stun.irian.at", 3478));
            stunServers.Add(new Tuple<string, int>("stun.it1.hr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ivao.aero", 3478));
            stunServers.Add(new Tuple<string, int>("stun.jappix.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.jumblo.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.justvoip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.kanet.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.kiwilink.co.nz", 3478));
            stunServers.Add(new Tuple<string, int>("stun.kundenserver.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.linea7.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.linphone.org", 3478));
            stunServers.Add(new Tuple<string, int>("stun.liveo.fr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.lowratevoip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.lugosoft.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.lundimatin.fr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.magnet.ie", 3478));
            stunServers.Add(new Tuple<string, int>("stun.manle.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.mgn.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.mit.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.mitake.com.tw", 3478));
            stunServers.Add(new Tuple<string, int>("stun.miwifi.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.modulus.gr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.mozcom.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.myvoiptraffic.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.mywatson.it", 3478));
            stunServers.Add(new Tuple<string, int>("stun.nas.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.neotel.co.za", 3478));
            stunServers.Add(new Tuple<string, int>("stun.netappel.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.netappel.fr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.netgsm.com.tr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.nfon.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.noblogs.org", 3478));
            stunServers.Add(new Tuple<string, int>("stun.noc.ams-ix.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.node4.co.uk", 3478));
            stunServers.Add(new Tuple<string, int>("stun.nonoh.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.nottingham.ac.uk", 3478));
            stunServers.Add(new Tuple<string, int>("stun.nova.is", 3478));
            stunServers.Add(new Tuple<string, int>("stun.nventure.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.on.net.mk", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ooma.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ooonet.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.oriontelekom.rs", 3478));
            stunServers.Add(new Tuple<string, int>("stun.outland-net.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ozekiphone.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.patlive.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.personal-voip.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.petcube.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.phone.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.phoneserve.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.pjsip.org", 3478));
            stunServers.Add(new Tuple<string, int>("stun.poivy.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.powerpbx.org", 3478));
            stunServers.Add(new Tuple<string, int>("stun.powervoip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ppdi.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.prizee.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.qq.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.qvod.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.rackco.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.rapidnet.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.rb-net.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.refint.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.remote-learner.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.rixtelecom.se", 3478));
            stunServers.Add(new Tuple<string, int>("stun.rockenstein.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.rolmail.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.rounds.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.rynga.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.samsungsmartcam.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.schlund.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.services.mozilla.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sigmavoip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sip.us", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sipdiscount.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sipgate.net", 10000));
            stunServers.Add(new Tuple<string, int>("stun.sipgate.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.siplogin.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sipnet.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sipnet.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.siportal.it", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sippeer.dk", 3478));
            stunServers.Add(new Tuple<string, int>("stun.siptraffic.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.skylink.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sma.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.smartvoip.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.smsdiscount.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.snafu.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.softjoys.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.solcon.nl", 3478));
            stunServers.Add(new Tuple<string, int>("stun.solnet.ch", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sonetel.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sonetel.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sovtest.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.speedy.com.ar", 3478));
            stunServers.Add(new Tuple<string, int>("stun.spokn.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.srce.hr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ssl7.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.stunprotocol.org", 3478));
            stunServers.Add(new Tuple<string, int>("stun.symform.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.symplicity.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.sysadminman.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.t-online.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.tagan.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.tatneft.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.teachercreated.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.tel.lu", 3478));
            stunServers.Add(new Tuple<string, int>("stun.telbo.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.telefacil.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.tis-dialog.ru", 3478));
            stunServers.Add(new Tuple<string, int>("stun.tng.de", 3478));
            stunServers.Add(new Tuple<string, int>("stun.twt.it", 3478));
            stunServers.Add(new Tuple<string, int>("stun.u-blox.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ucallweconn.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ucsb.edu", 3478));
            stunServers.Add(new Tuple<string, int>("stun.ucw.cz", 3478));
            stunServers.Add(new Tuple<string, int>("stun.uls.co.za", 3478));
            stunServers.Add(new Tuple<string, int>("stun.unseen.is", 3478));
            stunServers.Add(new Tuple<string, int>("stun.usfamily.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.veoh.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.vidyo.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.vipgroup.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.virtual-call.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.viva.gr", 3478));
            stunServers.Add(new Tuple<string, int>("stun.vivox.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.vline.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.vo.lu", 3478));
            stunServers.Add(new Tuple<string, int>("stun.vodafone.ro", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voicetrading.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voip.aebc.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voip.blackberry.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voip.eutelia.it", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voiparound.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipblast.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipbuster.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipbusterpro.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipcheap.co.uk", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipcheap.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipfibre.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipgain.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipgate.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipinfocenter.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipplanet.nl", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voippro.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipraider.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipstunt.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipwise.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voipzoom.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.vopium.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voxgratia.org", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voxox.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voys.nl", 3478));
            stunServers.Add(new Tuple<string, int>("stun.voztele.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.vyke.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.webcalldirect.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.whoi.edu", 3478));
            stunServers.Add(new Tuple<string, int>("stun.wifirst.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.wwdl.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun.xs4all.nl", 3478));
            stunServers.Add(new Tuple<string, int>("stun.xtratelecom.es", 3478));
            stunServers.Add(new Tuple<string, int>("stun.yesss.at", 3478));
            stunServers.Add(new Tuple<string, int>("stun.zadarma.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.zadv.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun.zoiper.com", 3478));
            stunServers.Add(new Tuple<string, int>("stun1.faktortel.com.au", 3478));
            stunServers.Add(new Tuple<string, int>("stun1.l.google.com", 19302));
            stunServers.Add(new Tuple<string, int>("stun1.voiceeclipse.net", 3478));
            stunServers.Add(new Tuple<string, int>("stun2.l.google.com", 19302));
            stunServers.Add(new Tuple<string, int>("stun3.l.google.com", 19302));
            stunServers.Add(new Tuple<string, int>("stun4.l.google.com", 19302));
            stunServers.Add(new Tuple<string, int>("stunserver.org", 3478));

            if (printMessages)
                Console.WriteLine("Contacting STUN servers to obtain your IP");

            foreach (Tuple<string, int> server in stunServers)
            {
                string host = server.Item1;
                int port = server.Item2;

                StunResult externalEndPoint = await StunClient.QueryAsync(host, port, socket);

                if (externalEndPoint.NetType == StunNetType.UdpBlocked)
                {
                    continue;
                }

                if (printMessages)
                    Console.WriteLine("Your firewall is {0}", externalEndPoint.NetType.ToString());

                return new P2pEndPoint()
                {
                    External = externalEndPoint.PublicEndPoint,
                    Internal = (socket.LocalEndPoint as IPEndPoint)
                };
            }

            if (printMessages)
                Console.WriteLine("Could not find a working STUN server");

            return null;
        }

        static int SleepTime(DateTime now)
        {
            int next = Seconds.Find(x => x > now.Second);

            return next - now.Second;
        }

        static async Task<UdtSocket> PeerConnectAsync(string remoteAddress, int[] remotePorts)
        {
            if (!Sockets.Any())
                return null;

            Console.WriteLine("Trying to connect to peer on different ports");

            UdtSocket udtSocket;
            CancellationTokenSource cts;
            var retry = 1;

            do
            {
                Console.WriteLine($"Try {retry++}:");
                Console.WriteLine();

                cts = new CancellationTokenSource();
                var taskList = remotePorts .Select((port, index) => ConnectAsync(Sockets[index], remoteAddress, port, cts.Token)).ToList();

                var now = InternetTime.Get();
                var sleepTimeToSync = SleepTime(now);

                Console.WriteLine("[{0}] - Waiting {1} sec to sync with other peer", now.ToLongTimeString(), sleepTimeToSync);

                await Task.Delay(TimeSpan.FromSeconds(sleepTimeToSync), cts.Token);

                Console.WriteLine("Connecting to other peer, please wait... ");
                udtSocket = await (await Task.WhenAny(taskList));
            } while (udtSocket == null);

            cts.Cancel();

            return udtSocket;
        }

        static async Task<UdtSocket> ConnectAsync(Socket socket, string remoteAddress, int remotePort, CancellationToken ct)
        {
            try
            {
                await GetExternalEndPointAsync(socket);

                var client = new UdtSocket(socket.AddressFamily, socket.SocketType);
                client.Bind(socket);

                Console.WriteLine("Trying to connect to {0}:{1}.  ", remoteAddress, remotePort);

                client.Connect(new IPEndPoint(IPAddress.Parse(remoteAddress), remotePort));

                Console.WriteLine("Connected successfully to {0}:{1}", remoteAddress, remotePort);

                if (ct.IsCancellationRequested)
                {
                    socket.Close();
                    ct.ThrowIfCancellationRequested();
                }

                return client;
            }
            catch (Exception e)
            {
                Console.WriteLine($"{e.Message.Replace(Environment.NewLine, ". ")} - Port:{remotePort}");
                return null;
            }
        }

        static async Task InitializeSocketsAsync()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            // Connecting to external to obtain IP and get firewall type
            var p2pEndPoint = await GetExternalEndPointAsync(socket);
            if (p2pEndPoint == null)
            {
                socket.Close();
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Tell your peer your IP is: {0}", p2pEndPoint.External.Address);
            Console.WriteLine("Initializing sockets...");

            var ports = new StringBuilder();

            for (var i = 0; i < 10; i++)
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Any, 0));

                p2pEndPoint = await GetExternalEndPointAsync(socket, false);
                if (p2pEndPoint == null)
                {
                    socket.Close();
                    continue;
                }

                ports.Append(p2pEndPoint.External.Port);
                ports.Append(",");

                Sockets.Add(socket);
            }

            if (ports.Length <= 0)
                return;

            ports.Remove(ports.Length - 1, 1);
            
            Console.WriteLine("Tell your peer your ports are: {0}", ports);
        }
    }
}