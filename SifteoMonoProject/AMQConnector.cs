using System;
using Apache.NMS;
using Apache.NMS.Util;
using Apache.NMS.ActiveMQ;
using System.Xml.Serialization;
using System.Xml;
using System.IO;

namespace SlideShow
{
	public class AMQConnector
	{
		private IConnection activeMQConnection;
		// The ActiveMQ connection for sending communication events
		private Apache.NMS.ISession activeMQSession;
		// See above
		private IMessageProducer activeMQProducer;
		// The producer of the events
		private IMessageConsumer activeMQConsumer;
		// Consume events from the CCC
		private bool ableToSendEvents = false;

		public AMQConnector ()
		{
		}

		public void Connect ()
		{
			while (!ableToSendEvents) {
				Uri connecturi = null;
				//if (textBoxSIPIPAddress.Text.StartsWith("ssl://"))
				//{
				Console.WriteLine ("Trying to connect to ActiveMQ broker ");
				//	connecturi = new Uri("activemq:" + textBoxSIPIPAddress.Text + ":" + textBoxSIPPort.Text + "?transport.ClientCertSubject=E%3DC.Rooney@mdx.ac.uk, CN%3DCommunication Tool"); // Connect to the ActiveMQ broker
				//}
				//else
				//{
				//log4.Debug(name + ": Trying to connect to ActiveMQ broker via non-secure connection");
				connecturi = new Uri ("activemq:tcp://localhost:61616"); // Connect to the ActiveMQ broker
				//}
				//Console.WriteLine("activeMQ::About to connect to " + connecturi);

				try {

					// NOTE: ensure the nmsprovider-activemq.config file exists in the executable folder.
					IConnectionFactory factory = new ConnectionFactory (connecturi);

					// Create a new connection and session for publishing events
					activeMQConnection = factory.CreateConnection ();
					activeMQSession = activeMQConnection.CreateSession ();

					IDestination destination = SessionUtil.GetDestination (activeMQSession, "topic://SIFTEO");
					//Console.WriteLine("activeMQ::Using destination: " + destination);




					// Create the producer
					activeMQProducer = activeMQSession.CreateProducer (destination);
					activeMQProducer.DeliveryMode = MsgDeliveryMode.Persistent;
					destination = SessionUtil.GetDestination (activeMQSession, "topic://XVR.CCC");
					activeMQConsumer = activeMQSession.CreateConsumer (destination);
					//activeMQConsumer.Listener += new MessageListener(OnCCCMessage);

					// Start the connection so that messages will be processed
					activeMQConnection.Start ();
					//activeMQProducer.Persistent = true;

					// Enable the sending of events
					//log4.Debug(name + ": ActiveMQ connected on topics XVR.CCC and XVR.SDK");
					ableToSendEvents = true;

				} catch (Exception exp) {
					// Report the problem in the output.log (Program Files (x86)\E-Semble\XVR 2012\XVR 2012\XVR_Data\output_log.txt)
					//Console.WriteLine("*** AN ACTIVE MQ ERROR OCCURED: " + exp.ToString() + " ***");
					//log4.Error(name + ": Error connecting to ActiveMQ broker: " + exp.Message);
					//log4.Error((exp.InnerException != null) ? exp.InnerException.StackTrace : "");

					Console.WriteLine (exp.Message);
				}
				System.Threading.Thread.Sleep (1000);
			}
		}

		public void send ()
		{
			SifteoEvent se = new SifteoEvent ();
			se.test = 1;

			XmlSerializer serializer = new XmlSerializer(se.GetType());
			MemoryStream stream = new MemoryStream();
			serializer.Serialize(stream, se);
			stream.Position = 0;

			// Send the event through to the SIP
			StreamReader m_reader = new StreamReader(stream);
			string text = m_reader.ReadToEnd();
			//SendEventOverSIP(text, _cEvent.GetType());

			ITextMessage request = activeMQSession.CreateTextMessage (text);
			request.NMSType = se.GetType().ToString ();
			activeMQProducer.Send (request);
		}
	

	}
}

