import { User } from '../types';
import './UserList.css';

interface UserListProps {
	users: User[];
	current_user_id: string;
}

export default function UserList({ users, current_user_id }: UserListProps) {
	return (
		<div className="user-list">
			<h3>Users ({users.length})</h3>
			<ul className="user-list-items">
				{users.map((user) => {
					const is_current = user.user_id === current_user_id;
					return (
						<li
							key={user.user_id}
							className={`user-list-item${is_current ? ' is-current' : ''}`}
						>
							<span
								className="user-list-dot"
								style={{ backgroundColor: user.colour }}
							/>
							<span>{user.name || 'Anonymous'}</span>
							{is_current && <span className="user-list-you">(you)</span>}
						</li>
					);
				})}
			</ul>
		</div>
	);
}
