import { useState, useEffect, useCallback, useRef } from 'react';
import './ConstrainPanel.css';

export interface ScreenInfo {
	label: string;
	left: number;
	top: number;
	width: number;
	height: number;
	is_primary: boolean;
	thumbnail_url?: string;
}

/**
 * Uses the Window Management API (Chrome/Edge 100+) to enumerate monitors.
 * Falls back to window.screen for browsers that don't support it.
 */
async function Detect_Screens(): Promise<ScreenInfo[]> {
	if ('getScreenDetails' in window) {
		try {
			const details = await (window as any).getScreenDetails();
			const screens: ScreenInfo[] = details.screens.map((s: any, i: number) => ({
				label: s.label || `Display ${i + 1}`,
				left: s.availLeft ?? s.left ?? 0,
				top: s.availTop ?? s.top ?? 0,
				width: s.availWidth ?? s.width ?? 1920,
				height: s.availHeight ?? s.height ?? 1080,
				is_primary: s.isPrimary ?? (i === 0),
			}));
			if (screens.length > 0) return screens;
		} catch {
			// Permission denied or API error — fall through
		}
	}

	return [{
		label: 'Display 1',
		left: 0,
		top: 0,
		width: window.screen.availWidth || window.screen.width,
		height: window.screen.availHeight || window.screen.height,
		is_primary: true,
	}];
}

/**
 * Captures a single video frame from a MediaStream and returns a data URL.
 */
function Grab_Frame(stream: MediaStream, width: number, height: number): Promise<string> {
	return new Promise((resolve, reject) => {
		const video = document.createElement('video');
		video.srcObject = stream;
		video.muted = true;
		video.playsInline = true;

		video.onloadeddata = () => {
			// Let a frame render
			requestAnimationFrame(() => {
				const canvas = document.createElement('canvas');
				canvas.width = width;
				canvas.height = height;
				const ctx = canvas.getContext('2d');
				if (!ctx) { reject(new Error('no 2d context')); return; }

				ctx.drawImage(video, 0, 0, width, height);
				const data_url = canvas.toDataURL('image/jpeg', 0.7);

				// Clean up
				video.srcObject = null;
				stream.getTracks().forEach((t) => t.stop());

				resolve(data_url);
			});
		};

		video.onerror = () => {
			stream.getTracks().forEach((t) => t.stop());
			reject(new Error('video error'));
		};

		video.play().catch(reject);
	});
}

interface Props {
	on_monitor_select?: (index: number, screen: ScreenInfo) => void;
}

export default function ConstrainPanel({ on_monitor_select }: Props) {
	const [screens, set_screens] = useState<ScreenInfo[]>([]);
	const [selected_index, set_selected_index] = useState(0);
	const [capturing, set_capturing] = useState(false);
	const screens_ref = useRef<ScreenInfo[]>([]);

	const refresh_screens = useCallback(async () => {
		const detected = await Detect_Screens();
		set_screens(detected);
		screens_ref.current = detected;

		const primary_idx = detected.findIndex((s) => s.is_primary);
		set_selected_index(primary_idx >= 0 ? primary_idx : 0);
	}, []);

	useEffect(() => {
		refresh_screens();
	}, [refresh_screens]);

	const handle_select = (index: number) => {
		set_selected_index(index);
		if (on_monitor_select && screens[index]) {
			on_monitor_select(index, screens[index]);
		}
	};

	// Capture a preview thumbnail for a specific monitor
	const capture_preview = async (index: number) => {
		if (capturing) return;
		set_capturing(true);

		try {
			const stream = await navigator.mediaDevices.getDisplayMedia({
				video: { displaySurface: 'monitor' } as any,
				audio: false,
			});

			// Grab a frame at thumbnail resolution
			const thumb_w = 320;
			const track = stream.getVideoTracks()[0];
			const settings = track.getSettings();
			const aspect = (settings.height || 1080) / (settings.width || 1920);
			const thumb_h = Math.round(thumb_w * aspect);

			const data_url = await Grab_Frame(stream, thumb_w, thumb_h);

			// Update the thumbnail for the clicked monitor
			set_screens((prev) => {
				const updated = [...prev];
				updated[index] = { ...updated[index], thumbnail_url: data_url };
				return updated;
			});
		} catch {
			// User cancelled the picker or error — ignore
		} finally {
			set_capturing(false);
		}
	};

	if (screens.length === 0) return null;

	// Bounding box of all screens for proportional layout
	const min_left = Math.min(...screens.map((s) => s.left));
	const min_top = Math.min(...screens.map((s) => s.top));
	const max_right = Math.max(...screens.map((s) => s.left + s.width));
	const max_bottom = Math.max(...screens.map((s) => s.top + s.height));
	const total_width = max_right - min_left;
	const total_height = max_bottom - min_top;

	// Scale to fit the container, with some padding
	const container_max_w = 440;
	const container_max_h = 200;
	const scale = Math.min(container_max_w / total_width, container_max_h / total_height) * 0.9;
	const layout_w = total_width * scale;
	const layout_h = total_height * scale;

	return (
		<div className="constrain-panel">
			<h3>Constrain Mouse to:</h3>

			<div className="monitor-layout" style={{ width: layout_w, height: layout_h }}>
				{screens.map((screen, idx) => {
					const x = (screen.left - min_left) * scale;
					const y = (screen.top - min_top) * scale;
					const w = screen.width * scale;
					const h = screen.height * scale;
					const is_selected = idx === selected_index;

					return (
						<button
							key={idx}
							className={`monitor-thumb${is_selected ? ' selected' : ''}`}
							style={{ left: x, top: y, width: w, height: h }}
							onClick={() => handle_select(idx)}
							title={`${screen.label} — ${screen.width}×${screen.height}${screen.is_primary ? ' (Primary)' : ''}`}
						>
							{screen.thumbnail_url ? (
								<img
									src={screen.thumbnail_url}
									alt={screen.label}
									className="monitor-preview"
									draggable={false}
								/>
							) : (
								<div className="monitor-placeholder">
									<span className="monitor-icon">🖥</span>
								</div>
							)}

							<div className="monitor-label-bar">
								<span className="monitor-name">
									{screen.is_primary ? '★ ' : ''}{screen.label}
								</span>
								<span className="monitor-res">{screen.width}×{screen.height}</span>
							</div>

							{/* Capture button overlay */}
							{!screen.thumbnail_url && (
								<button
									className="monitor-capture-btn"
									onClick={(e) => { e.stopPropagation(); capture_preview(idx); }}
									disabled={capturing}
									title="Capture preview"
								>
									📷
								</button>
							)}
						</button>
					);
				})}
			</div>

			{screens.length > 1 && (
				<p className="monitor-selected-hint">
					Selected: <strong>{screens[selected_index]?.label}</strong>
				</p>
			)}
		</div>
	);
}

