# app.py

# Flask application providing a REST API for the Gormaz AR project.
# It manages user registration, graffiti scan increments, and session statistics.
from flask import Flask, jsonify, request
from flask_cors import CORS
from pymongo import MongoClient

app = Flask(__name__)
CORS(app)  # Enable Cross-Origin Resource Sharing for all routes

# -------------------------------------------------------------------
# Database setup
# -------------------------------------------------------------------

# Connect to local MongoDB instance
client = MongoClient('mongodb://localhost:27017/')
db = client['GormazAR']

# Collections for graffiti data, users, and global statistics
images_col = db['graffiti']
users_col  = db['users']
stats_col  = db['stats']

# -------------------------------------------------------------------
# Initialize global statistics document if it does not exist
# Tracks:
#   - unique_users: how many distinct devices have registered
#   - users_completed: how many users have scanned all graffiti
#   - sessions_count: total number of sessions ended
#   - average_session_time: running average of session durations
# -------------------------------------------------------------------
if stats_col.count_documents({"_id": "global"}) == 0:
    stats_col.insert_one({
        "_id": "global",
        "unique_users": 0,
        "users_completed": 0,
        "sessions_count": 0,
        "average_session_time": 0.0
    })

# -------------------------------------------------------------------
# Ensure initial graffiti documents exist
# Each doc has:
#   id      – identifier matching the AR reference image name
#   name    – human-readable description
#   scans   – counter of total scans
# -------------------------------------------------------------------
initial_docs = [
    {"id": "irlSoldier", "name": "Soldier in north wall", "scans": 0},
    {"id": "irlDate",    "name": "Gothic inscription in north wall", "scans": 0},
    {"id": "irlMonk",    "name": "Pointing monk in hastial", "scans": 0}
]
for doc in initial_docs:
    if images_col.count_documents({"id": doc["id"]}, limit=1) == 0:
        images_col.insert_one(doc)

# -------------------------------------------------------------------
# Route: Home
# Returns a welcome message
# -------------------------------------------------------------------
@app.route('/')
def home():
    return jsonify({"message": "Welcome to GormazAR's API"}), 200

# -------------------------------------------------------------------
# Route: Register User
# POST /registerUser/<user_id>
# Registers a new user/device if not already present.
# Increments unique_users if this is a new registration.
# -------------------------------------------------------------------
@app.route('/registerUser/<user_id>', methods=['POST'])
def register_user(user_id):
    # Only insert if the user_id is new
    if not users_col.find_one({"user_id": user_id}):
        users_col.insert_one({
            "user_id": user_id,
            "scanned": [],      # list of graffiti IDs scanned by this user
            "completed": False  # flag marking if user scanned all graffiti
        })
        # Update global unique_users count
        stats_col.update_one(
            {"_id": "global"},
            {"$inc": {"unique_users": 1}}
        )
        return jsonify({"message": f"User {user_id} successfully registered"}), 201

    # User was already registered
    return jsonify({"message": "User already registered"}), 200

# -------------------------------------------------------------------
# Route: Increment Scan Counter
# POST /increment/<doc_id>
# Increments the scan count for a graffiti document (doc_id),
# records the scan under the given user_id, and flags completion when
# user scans all three.
# -------------------------------------------------------------------
@app.route('/increment/<doc_id>', methods=['POST'])
def increment_counter(doc_id):
    user_id = request.form.get("user_id")
    if not user_id:
        return jsonify({"error": "Missing user_id in request."}), 400

    # Increment the scans counter on the graffiti document
    result = images_col.update_one(
        {"id": doc_id},
        {"$inc": {"scans": 1}}
    )
    if result.modified_count == 0:
        return jsonify({"error": f"Could not increment scans for {doc_id}"}), 400

    # Retrieve updated graffiti document
    doc = images_col.find_one({"id": doc_id})

    # Add this doc_id to user's scanned list if not already present
    users_col.update_one(
        {"user_id": user_id},
        {"$addToSet": {"scanned": doc_id}}
    )

    # Check if user has now scanned all three graffiti
    user = users_col.find_one({"user_id": user_id})
    scanned = user.get("scanned", [])
    if len(scanned) == 3 and not user.get("completed", False):
        # Mark user as completed and update global counter
        users_col.update_one(
            {"user_id": user_id},
            {"$set": {"completed": True}}
        )
        stats_col.update_one(
            {"_id": "global"},
            {"$inc": {"users_completed": 1}}
        )

    # Return the updated stats for this graffiti and user
    return jsonify({
        "name":         doc["name"],
        "scans":        doc["scans"],
        "user_scanned": scanned
    }), 200

# -------------------------------------------------------------------
# Route: End Session
# POST /endSession/<user_id>
# Receives the session duration, updates global session count and
# running average session time.
# -------------------------------------------------------------------
@app.route('/endSession/<user_id>', methods=['POST'])
def end_session(user_id):
    # Parse duration from form data
    duration = request.form.get("duration")
    if duration is None:
        return jsonify({"error": "Missing 'duration' field."}), 400
    try:
        duration = float(duration)
    except ValueError:
        return jsonify({"error": "Invalid 'duration' value."}), 400

    # Retrieve current stats
    stats = stats_col.find_one({"_id": "global"})
    current_count = stats.get("sessions_count", 0)
    current_avg   = stats.get("average_session_time", 0.0)

    # Compute new running average
    new_count = current_count + 1
    new_avg   = (current_avg * current_count + duration) / new_count

    # Update stats collection
    stats_col.update_one(
        {"_id": "global"},
        {"$set": {
            "sessions_count": new_count,
            "average_session_time": new_avg
        }}
    )

    # Return updated session info
    return jsonify({
        "session_duration":     duration,
        "average_session_time": new_avg
    }), 200

# -------------------------------------------------------------------
# Application entry point
# Runs the Flask server on all interfaces at port 5000 in debug mode
# -------------------------------------------------------------------
if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
