// Session and user types for Three Blind Mice

export interface User {
	user_id: string;
	name: string;
	colour: string;
}

export interface SessionState {
	session_code: string;
	is_host: boolean;
	users: User[];
	connected: boolean;
}

// Colour palette for auto-assigning cursor colours
export const CURSOR_COLOURS = [
	'#FF6B35', '#4ECDC4', '#45B7D1', '#96CEB4',
	'#FFEAA7', '#DDA0DD', '#98D8C8', '#F7DC6F',
	'#BB8FCE', '#85C1E9', '#F1948A', '#82E0AA',
];

export function Generate_Session_Code(): string {
	const chars = 'abcdefghijklmnopqrstuvwxyz0123456789';
	let code = '';
	const array = new Uint8Array(6);
	crypto.getRandomValues(array);
	for (const byte of array) {
		code += chars[byte % chars.length];
	}
	return code;
}
