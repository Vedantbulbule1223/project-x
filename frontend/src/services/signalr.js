import * as signalR from '@microsoft/signalr'

let connection = null

export function useSignalR() {
  function connect(token) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/pulse', { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connection.start()
      .then(() => console.log('[SignalR] Connected to Pulse Hub'))
      .catch(e => console.error('[SignalR] Connection failed:', e))

    return connection
  }

  function disconnect() {
    if (connection) connection.stop()
  }

  function onEvent(event, cb) {
    if (connection) connection.on(event, cb)
  }

  function offEvent(event, cb) {
    if (connection) connection.off(event, cb)
  }

  return { connect, disconnect, onEvent, offEvent, getConnection: () => connection }
}
