using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace Receiver.RabbitMQ
{
    //封装RabbitMQ的连接、发送和接收功能
    public class RabbitMqService
    {
        private readonly string _hostName;
        private readonly string _queueName;
        private IConnection _connection;
        private IModel _channel;

        public RabbitMqService(string hostName, string queueName)
        {
            _hostName = hostName;
            _queueName = queueName;
            InitializeConnection();
        }

        //初始化连接:设置连接到RabbitMQ服务器并声明队列。
        private void InitializeConnection()
        {
            var factory = new ConnectionFactory() { HostName = _hostName };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: _queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
        }

        //发送消息:将消息编码为字节数组，并发布到队列。
        public void SendMessage(KeyValuePair<string, object> message)
        {
            var json = JsonConvert.SerializeObject(message);
            var body = Encoding.UTF8.GetBytes(json);
            _channel.BasicPublish(exchange: "", routingKey: _queueName, basicProperties: null, body: body);
        }

        //接收消息:使用 EventingBasicConsumer 接收消息，并将消息传递给回调函数。
        public void StartReceivingMessages(Action<KeyValuePair<string, object>> onMessageReceived)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                var message = JsonConvert.DeserializeObject<KeyValuePair<string, object>>(json);
                onMessageReceived(message);
            };
            _channel.BasicConsume(queue: _queueName, autoAck: true, consumer: consumer);
        }

        //关闭连接
        public void Dispose()
        {
            //关闭RabbitMQ通道，释放与队列的连接资源。
            _channel?.Close();
            //关闭RabbitMQ连接，释放与服务器的连接资源。
            _connection?.Close();
        }
    }
}
