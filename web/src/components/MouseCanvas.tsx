import { useRef, useState, useCallback, useEffect } from 'react';
import './MouseCanvas.css';

interface MouseCanvasProps {
	aspect_ratio?: number;
	on_cursor_move: (x: number, y: number, button: number) => void;
	user_name: string;
	user_colour: string;
}

export default function MouseCanvas({
	aspect_ratio = 16 / 9,
	on_cursor_move,
	user_name,
	user_colour,
}: MouseCanvasProps) {
	const canvas_ref = useRef<HTMLDivElement>(null);
	const [is_locked, set_is_locked] = useState(false);
	const [cursor_x, set_cursor_x] = useState(0.5);
	const [cursor_y, set_cursor_y] = useState(0.5);
	const [button_state, set_button_state] = useState(0);

	// Track accumulated movement while pointer-locked
	const accum_ref = useRef({ x: 0.5, y: 0.5 });

	// Resize the canvas div to fill parent while maintaining aspect ratio
	const [canvas_size, set_canvas_size] = useState({ width: 640, height: 360 });
	const wrapper_ref = useRef<HTMLDivElement>(null);

	useEffect(() => {
		const wrapper = wrapper_ref.current;
		if (!wrapper) return;

		const observer = new ResizeObserver((entries) => {
			for (const entry of entries) {
				const { width: max_w, height: max_h } = entry.contentRect;
				if (max_w <= 0 || max_h <= 0) return;

				let w = max_w;
				let h = w / aspect_ratio;
				if (h > max_h) {
					h = max_h;
					w = h * aspect_ratio;
				}
				set_canvas_size({ width: Math.floor(w), height: Math.floor(h) });
			}
		});

		observer.observe(wrapper);
		return () => observer.disconnect();
	}, [aspect_ratio]);

	// Listen for pointer lock changes
	useEffect(() => {
		const on_lock_change = () => {
			const locked = document.pointerLockElement === canvas_ref.current;
			set_is_locked(locked);
			if (!locked) {
				// Reset button state when unlocking
				set_button_state(0);
			}
		};

		document.addEventListener('pointerlockchange', on_lock_change);
		return () => document.removeEventListener('pointerlockchange', on_lock_change);
	}, []);

	// Handle mouse move — works in both locked and unlocked modes
	const handle_mouse_move = useCallback((e: React.MouseEvent) => {
		const el = canvas_ref.current;
		if (!el) return;

		let nx: number;
		let ny: number;

		if (is_locked) {
			// In pointer lock, use movementX/Y accumulated on normalised coords
			const acc = accum_ref.current;
			acc.x = Math.max(0, Math.min(1, acc.x + e.movementX / canvas_size.width));
			acc.y = Math.max(0, Math.min(1, acc.y + e.movementY / canvas_size.height));
			nx = acc.x;
			ny = acc.y;
		} else {
			const rect = el.getBoundingClientRect();
			nx = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
			ny = Math.max(0, Math.min(1, (e.clientY - rect.top) / rect.height));
		}

		set_cursor_x(nx);
		set_cursor_y(ny);
		on_cursor_move(nx, ny, button_state);
	}, [is_locked, canvas_size, button_state, on_cursor_move]);

	const handle_click = useCallback(() => {
		if (!is_locked && canvas_ref.current) {
			canvas_ref.current.requestPointerLock();
		}
	}, [is_locked]);

	const handle_mouse_down = useCallback((e: React.MouseEvent) => {
		if (e.button === 0) {
			set_button_state(1);
			on_cursor_move(cursor_x, cursor_y, 1);
		}
	}, [cursor_x, cursor_y, on_cursor_move]);

	const handle_mouse_up = useCallback((e: React.MouseEvent) => {
		if (e.button === 0) {
			set_button_state(0);
			on_cursor_move(cursor_x, cursor_y, 0);
		}
	}, [cursor_x, cursor_y, on_cursor_move]);

	const instruction_text = is_locked
		? 'Press Esc to release cursor'
		: 'Click to lock cursor';

	return (
		<div className="mouse-canvas-wrapper" ref={wrapper_ref}>
			<div
				className={`mouse-canvas${is_locked ? ' locked' : ''}`}
				ref={canvas_ref}
				style={{ width: canvas_size.width, height: canvas_size.height }}
				onClick={handle_click}
				onMouseMove={handle_mouse_move}
				onMouseDown={handle_mouse_down}
				onMouseUp={handle_mouse_up}
			>
				<div className="mouse-canvas-grid" />

				<div className="mouse-canvas-instruction">{instruction_text}</div>

				{/* User's own cursor dot */}
				<div
					className="mouse-canvas-cursor"
					style={{
						left: `${cursor_x * 100}%`,
						top: `${cursor_y * 100}%`,
						backgroundColor: user_colour,
					}}
				>
					<span className="mouse-canvas-cursor-label">{user_name}</span>
				</div>
			</div>
		</div>
	);
}
