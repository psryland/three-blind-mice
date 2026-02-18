const { WebPubSubServiceClient } = require("@azure/web-pubsub");

const SESSION_CODE_REGEX = /^[a-zA-Z0-9]{4,8}$/;
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
		const session = req.query.session || "default";
		const user = req.query.user || "anon";

		// Validate session code
		if (!SESSION_CODE_REGEX.test(session)) {
			context.res = {
				status: 400,
				body: { error: "Invalid session code. Must be 4-8 alphanumeric characters." }
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
				`webpubsub.joinLeaveGroup.${session}`,
				`webpubsub.sendToGroup.${session}`
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
