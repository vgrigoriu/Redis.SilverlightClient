﻿using System;
using System.Net.Sockets;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace Redis.SilverlightClient.Sockets
{
    public class SocketReceiver
    {
        private readonly Socket connectedSocket;
        private readonly SocketAsyncEventArgs socketEventArgs;

        public SocketReceiver(Socket connectedSocket, SocketAsyncEventArgs socketEventArgs)
        {
            if (connectedSocket == null)
                throw new ArgumentNullException("connectedSocket");

            if (socketEventArgs == null)
                throw new ArgumentNullException("socketEventArgs");

            this.connectedSocket = connectedSocket;
            this.socketEventArgs = socketEventArgs;
        }

        public IObservable<string> Receive(byte[] buffer, IScheduler scheduler)
        {
            return Observable.Create<string>(observer =>
            {
                var disposable = new CompositeDisposable();

                var disposableEventSubscription = socketEventArgs.CompletedObservable().ObserveOn(scheduler).Subscribe(_ =>
                {
                    SendNotificationToObserver(observer, socketEventArgs);
                });

                var disposableActions = scheduler.Schedule(() =>
                {
                    socketEventArgs.SetBuffer(buffer, 0, buffer.Length);
                    if (!connectedSocket.ReceiveAsync(socketEventArgs))
                    {
                        SendNotificationToObserver(observer, socketEventArgs);
                    }
                });

                disposable.Add(disposableEventSubscription);
                disposable.Add(disposableActions);

                return disposable;
            });
        }

        private void SendNotificationToObserver(IObserver<string> observer, SocketAsyncEventArgs socketEvent)
        {
            if (socketEvent.SocketError == SocketError.Success)
            {
                observer.OnNext(Encoding.UTF8.GetString(socketEvent.Buffer, 0, socketEvent.BytesTransferred));
                observer.OnCompleted();
            }
            else
            {
                observer.OnError(new RedisException(socketEvent.SocketError));
            }
        }
    }
}
