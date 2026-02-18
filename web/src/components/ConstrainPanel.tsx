import { useState, useCallback } from 'react';
import type { MonitorInfo, WindowInfo, HostConstraintMessage } from '../services/pubsub';
import './ConstrainPanel.css';

type ConstraintMode = 'monitors' | 'windows' | 'rectangle';

interface Props {
	monitors: MonitorInfo[];
	thumbnails: Map<number, string>;
	windows: WindowInfo[];
	on_constraint_select: (constraint: Omit<HostConstraintMessage, 'type'>) => void;
}

export default function ConstrainPanel({ monitors, thumbnails, windows, on_constraint_select }: Props) {
	const [active_tab, set_active_tab] = useState<ConstraintMode>('monitors');
	const [selected_monitor, set_selected_monitor] = useState(0);
	const [selected_window, set_selected_window] = useState(0);
	const [rect, set_rect] = useState({ left: 0, top: 0, width: 1920, height: 1080 });

	const handle_monitor_select = useCallback((index: number) => {
		set_selected_monitor(index);
		on_constraint_select({ mode: 'monitor', monitor_index: index });
	}, [on_constraint_select]);

	const handle_window_select = useCallback((index: number) => {
		set_selected_window(index);
		const win = windows[index];
		if (win) {
			on_constraint_select({ mode: 'window', window_title: win.title });
		}
	}, [windows, on_constraint_select]);

	const handle_rect_apply = useCallback(() => {
		on_constraint_select({
			mode: 'rectangle',
			left: rect.left,
			top: rect.top,
			width: rect.width,
			height: rect.height,
		});
	}, [rect, on_constraint_select]);

	// Waiting for host app data
	if (monitors.length === 0) {
		return (
			<div className="constrain-panel">
				<h3>Constrain Mouse to:</h3>
				<div className="constrain-placeholder">
					<span className="monitor-icon">🖥</span>
					<p>Waiting for host app…</p>
				</div>
			</div>
		);
	}

	// Bounding box of all monitors for proportional layout
	const min_left = Math.min(...monitors.map((m) => m.left));
	const min_top = Math.min(...monitors.map((m) => m.top));
	const max_right = Math.max(...monitors.map((m) => m.left + m.width));
	const max_bottom = Math.max(...monitors.map((m) => m.top + m.height));
	const total_width = max_right - min_left;
	const total_height = max_bottom - min_top;

	const container_max_w = 440;
	const container_max_h = 200;
	const scale = Math.min(container_max_w / total_width, container_max_h / total_height) * 0.9;
	const layout_w = total_width * scale;
	const layout_h = total_height * scale;

	return (
		<div className="constrain-panel">
			<h3>Constrain Mouse to:</h3>

			<div className="constrain-tabs">
				<button
					className={`constrain-tab${active_tab === 'monitors' ? ' active' : ''}`}
					onClick={() => set_active_tab('monitors')}
				>
					Monitors
				</button>
				<button
					className={`constrain-tab${active_tab === 'windows' ? ' active' : ''}`}
					onClick={() => set_active_tab('windows')}
				>
					Windows
				</button>
				<button
					className={`constrain-tab${active_tab === 'rectangle' ? ' active' : ''}`}
					onClick={() => set_active_tab('rectangle')}
				>
					Rectangle
				</button>
			</div>

			{/* Monitors tab */}
			{active_tab === 'monitors' && (
				<>
					<div className="monitor-layout" style={{ width: layout_w, height: layout_h }}>
						{monitors.map((monitor, idx) => {
							const x = (monitor.left - min_left) * scale;
							const y = (monitor.top - min_top) * scale;
							const w = monitor.width * scale;
							const h = monitor.height * scale;
							const thumb = thumbnails.get(monitor.index);

							return (
								<button
									key={monitor.index}
									className={`monitor-thumb${idx === selected_monitor ? ' selected' : ''}`}
									style={{ left: x, top: y, width: w, height: h }}
									onClick={() => handle_monitor_select(idx)}
									title={`${monitor.device} — ${monitor.width}×${monitor.height}${monitor.is_primary ? ' (Primary)' : ''}`}
								>
									{thumb ? (
										<img
											src={thumb}
											alt={monitor.device}
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
											{monitor.is_primary ? '★ ' : ''}{monitor.device}
										</span>
										<span className="monitor-res">{monitor.width}×{monitor.height}</span>
									</div>
								</button>
							);
						})}
					</div>

					{monitors.length > 1 && (
						<p className="monitor-selected-hint">
							Selected: <strong>{monitors[selected_monitor]?.device}</strong>
						</p>
					)}
				</>
			)}

			{/* Windows tab */}
			{active_tab === 'windows' && (
				<div className="window-list">
					{windows.length === 0 ? (
						<p className="window-list-empty">No windows reported by host</p>
					) : (
						windows.map((win, idx) => (
							<button
								key={idx}
								className={`window-item${idx === selected_window ? ' selected' : ''}`}
								onClick={() => handle_window_select(idx)}
								title={`${win.width}×${win.height} on monitor ${win.monitor_index}`}
							>
								<span className="window-title">{win.title}</span>
								<span className="window-res">{win.width}×{win.height}</span>
							</button>
						))
					)}
				</div>
			)}

			{/* Rectangle tab */}
			{active_tab === 'rectangle' && (
				<div className="rect-inputs">
					<div className="rect-row">
						<label>
							Left
							<input
								type="number"
								value={rect.left}
								onChange={(e) => set_rect((r) => ({ ...r, left: Number(e.target.value) }))}
							/>
						</label>
						<label>
							Top
							<input
								type="number"
								value={rect.top}
								onChange={(e) => set_rect((r) => ({ ...r, top: Number(e.target.value) }))}
							/>
						</label>
					</div>
					<div className="rect-row">
						<label>
							Width
							<input
								type="number"
								value={rect.width}
								onChange={(e) => set_rect((r) => ({ ...r, width: Number(e.target.value) }))}
							/>
						</label>
						<label>
							Height
							<input
								type="number"
								value={rect.height}
								onChange={(e) => set_rect((r) => ({ ...r, height: Number(e.target.value) }))}
							/>
						</label>
					</div>
					<button className="btn-primary rect-apply" onClick={handle_rect_apply}>
						Apply
					</button>
				</div>
			)}
		</div>
	);
}
