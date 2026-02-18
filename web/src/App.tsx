import { useState, useCallback, useEffect, useRef, useMemo } from 'react';
import SessionPanel from './components/SessionPanel';
import MouseCanvas from './components/MouseCanvas';
import UserList from './components/UserList';
import ConstrainPanel from './components/ConstrainPanel';
import DownloadPanel from './components/DownloadPanel';
import { PubSubClient, PubSubMessage } from './services/pubsub';
import { User, CURSOR_COLOURS } from './types';
import './App.css';

const STORAGE_KEY_NAME = 'tbm_user_name';

function Generate_User_Id(): string {
	const chars = 'abcdefghijklmnopqrstuvwxyz0123456789';
	let id = '';
	const array = new Uint8Array(8);
	crypto.getRandomValues(array);
	for (const byte of array) {
		id += chars[byte % chars.length];
	}
	return id;
}

function App() {
	const [session_code, set_session_code] = useState<string | null>(null);
	const [is_host, set_is_host] = useState(false);
	const [connected, set_connected] = useState(false);
	const [users, set_users] = useState<User[]>([]);
	const [aspect_ratio, set_aspect_ratio] = useState(16 / 9);
	const [user_name, set_user_name] = useState(() => {
		return localStorage.getItem(STORAGE_KEY_NAME) ?? '';
	});

	// Stable user identity for the session
	const user_id = useMemo(() => Generate_User_Id(), []);
	const user_colour = useMemo(() => CURSOR_COLOURS[Math.floor(Math.random() * CURSOR_COLOURS.length)], []);

	const client_ref = useRef<PubSubClient | null>(null);

	// Persist name to localStorage when it changes
	useEffect(() => {
		localStorage.setItem(STORAGE_KEY_NAME, user_name);
	}, [user_name]);

	const handle_message = useCallback((msg: PubSubMessage) => {
		switch (msg.type) {
			case 'join':
				set_users((prev) => {
					if (prev.some((u) => u.user_id === msg.user_id)) return prev;
					return [...prev, { user_id: msg.user_id, name: msg.name, colour: msg.colour }];
				});
				break;
			case 'leave':
				set_users((prev) => prev.filter((u) => u.user_id !== msg.user_id));
				break;
			case 'cursor':
				// Update user entry if we see a cursor from someone not yet in the list
				set_users((prev) => {
					const idx = prev.findIndex((u) => u.user_id === msg.user_id);
					if (idx === -1) {
						return [...prev, { user_id: msg.user_id, name: msg.name, colour: msg.colour }];
					}
					return prev;
				});
				break;
			case 'host_config':
				if (msg.aspect_ratio > 0) {
					set_aspect_ratio(msg.aspect_ratio);
				}
				break;
		}
	}, []);

	const handle_join = useCallback((code: string, host: boolean) => {
		set_session_code(code);
		set_is_host(host);
		set_users([{ user_id, name: user_name || 'Anonymous', colour: user_colour }]);
		set_aspect_ratio(16 / 9);

		const client = new PubSubClient();
		client_ref.current = client;

		client.on_message(handle_message);
		client.on_connection_change((is_connected) => set_connected(is_connected));
		client.connect(code, user_id, user_name || 'Anonymous', user_colour);
	}, [user_id, user_name, user_colour, handle_message]);

	const handle_leave = useCallback(() => {
		client_ref.current?.disconnect();
		client_ref.current = null;
		set_session_code(null);
		set_is_host(false);
		set_connected(false);
		set_users([]);
	}, []);

	const handle_cursor_move = useCallback((x: number, y: number, button: number) => {
		client_ref.current?.send_cursor(x, y, button);
	}, []);

	// Clean up on unmount
	useEffect(() => {
		return () => {
			client_ref.current?.disconnect();
		};
	}, []);

	const show_canvas = session_code !== null && !is_host && connected;

	return (
		<div className="app">
			<header className="app-header">
				<h1>Three Blind Mice</h1>
				<span className="app-header-url">https://three-blind-mice.rylogic.co.nz</span>
			</header>

			<div className="app-body">
				<div className="app-layout">
					<div className="app-left">
						<div className="name-input">
							<label htmlFor="user-name">Your Name</label>
							<input
								id="user-name"
								type="text"
								placeholder="Enter your name"
								value={user_name}
								onChange={(e) => set_user_name(e.target.value)}
								maxLength={32}
								disabled={session_code !== null}
							/>
						</div>

						<ConstrainPanel />

						{show_canvas && (
							<MouseCanvas
								aspect_ratio={aspect_ratio}
								on_cursor_move={handle_cursor_move}
								user_name={user_name || 'Anonymous'}
								user_colour={user_colour}
							/>
						)}

						{session_code && !connected && (
							<div className="canvas-placeholder">
								<p>Connecting to session <strong>{session_code}</strong>…</p>
							</div>
						)}

						{session_code && is_host && connected && (
							<div className="canvas-placeholder">
								<p>
									Hosting session <strong>{session_code}</strong>
								</p>
								<p className="placeholder-hint">Run the desktop overlay to receive mouse input</p>
							</div>
						)}
					</div>

					<div className="app-right">
						<SessionPanel
							on_join={handle_join}
							on_leave={handle_leave}
							session_code={session_code}
						/>

						{session_code && (
							<UserList users={users} current_user_id={user_id} />
						)}

						<DownloadPanel session_code={session_code} is_host={is_host} />
					</div>
				</div>
			</div>
		</div>
	);
}

export default App;
