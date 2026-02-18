const { WebPubSubServiceClient } = require("@azure/web-pubsub");

const ROOM_CODE_REGEX = /^[a-zA-Z0-9]{4,8}$/;
const USER_ID_REGEX = /^[a-zA-Z0-9_-]{1,50}$/;

module.exports = async function (context, req) {
	const connectionString = process.env.WEBPUBSUB_CONNECTION_STRING;
	
	if (!connectionString) {
		context.res = {
			status: 500,
			body: { error: "WEBPUBSUB_CONNECTION_STRING not configured" }
		};
		return;
	}

	try {
		const room = req.query.room || "default";
		const user = req.query.user || "anon";

		// Validate room code
		if (!ROOM_CODE_REGEX.test(room)) {
			context.res = {
				status: 400,
				body: { error: "Invalid room code. Must be 4-8 alphanumeric characters." }
			};
			return;
		}

		// Validate user ID
		if (!USER_ID_REGEX.test(user)) {
			context.res = {
				status: 400,
				body: { error: "Invalid user ID. Must be 1-50 alphanumeric, dash, or underscore characters." }
			};
			return;
		}

		const client = new WebPubSubServiceClient(connectionString, "tbm");

		const token = await client.getClientAccessToken({
			userId: user,
			roles: [
				`webpubsub.joinLeaveGroup.${room}`,
				`webpubsub.sendToGroup.${room}`
			]
		});

		context.res = {
			status: 200,
			body: {
				url: token.url
			}
		};
	} catch (error) {
		context.res = {
			status: 500,
			body: { error: error.message }
		};
	}
};
