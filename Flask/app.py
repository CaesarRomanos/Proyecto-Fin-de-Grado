from flask import Flask, jsonify, request
from flask_cors import CORS
from pymongo import MongoClient

app = Flask(__name__)
CORS(app)

# Conexión a MongoDB
client = MongoClient('mongodb://localhost:27017/')
db = client['GormazAR']
collection = db['Gormaz_coleccion']  # Colección para imágenes

# Colección para registrar usuarios
users_collection = db['users']
# Colección para estadísticas globales
stats_collection = db['stats']

# Inicializa el documento de estadísticas global si no existe
if stats_collection.count_documents({"_id": "global"}) == 0:
    stats_collection.insert_one({"_id": "global", "unique_users": 0, "users_completed": 0, "average_time": 0})

# Inicialización de los documentos de imágenes si no existen
initial_docs = [
    {"id": "irlSoldier", "name": "Soldier in north wall", "scans": 0},
    {"id": "irlDate", "name": "Gothic inscryption in north wall", "scans": 0},
    {"id": "irlMonk", "name": "Pointing monk in hastial", "scans": 0}
]

for doc in initial_docs:
    if collection.count_documents({"id": doc["id"]}, limit=1) == 0:
        collection.insert_one(doc)

# Endpoint de bienvenida
@app.route('/')
def home():
    return jsonify({"message": "Bienvenido a la API de Gormaz"}), 200

# Endpoint para registrar un usuario único al iniciar la aplicación
@app.route('/registerUser/<user_id>', methods=['POST'])
def register_user(user_id):
    if not users_collection.find_one({"user_id": user_id}):
        # Registra al usuario con campos iniciales, incluyendo totalTime en 0
        users_collection.insert_one({
            "user_id": user_id,
            "scanned": [],
            "completed": False,
            "totalTime": 0
        })
        stats_collection.update_one({"_id": "global"}, {"$inc": {"unique_users": 1}})
        return jsonify({"message": f"Usuario {user_id} registrado exitosamente."}), 201
    return jsonify({"message": "Usuario ya registrado."}), 200

# Endpoint para incrementar el contador de una imagen y actualizar registros de usuario
@app.route('/increment/<doc_id>', methods=['POST'])
def increment_counter(doc_id):
    user_id = request.form.get("user_id")
    if not user_id:
        return jsonify({"error": "Falta el user_id en la solicitud."}), 400

    # Incrementa el contador en la colección de imágenes
    result = collection.update_one({"id": doc_id}, {"$inc": {"scans": 1}})
    if result.modified_count:
        doc = collection.find_one({"id": doc_id})
    else:
        return jsonify({"error": f"No se pudo incrementar el contador para {doc_id}"}), 400

    # Actualiza el registro del usuario:
    # Se agrega el doc_id a la lista de imágenes escaneadas (sin duplicados)
    users_collection.update_one({"user_id": user_id}, {"$addToSet": {"scanned": doc_id}})
    
    # Recupera el registro actualizado del usuario
    user = users_collection.find_one({"user_id": user_id})
    scanned_images = user.get("scanned", [])
    # Si el usuario ha completado los tres escaneos y aún no se marcó como completado
    if len(scanned_images) == 3 and user.get("completed", False) is False:
        users_collection.update_one({"user_id": user_id}, {"$set": {"completed": True}})
        stats_collection.update_one({"_id": "global"}, {"$inc": {"users_completed": 1}})

    return jsonify({
        "name": doc["name"],
        "scans": doc["scans"],
        "user_scanned": scanned_images
    })

# Nuevo endpoint para actualizar el tiempo de sesión del usuario
@app.route('/updateTime/<user_id>', methods=['POST'])
def update_session_time(user_id):
    session_time = request.form.get("session_time")
    if session_time is None:
        return jsonify({"error": "Falta el session_time en la solicitud."}), 400
    try:
        session_time = float(session_time)
    except ValueError:
        return jsonify({"error": "session_time debe ser un número."}), 400

    # Actualiza el campo totalTime del usuario
    result = users_collection.update_one({"user_id": user_id}, {"$inc": {"totalTime": session_time}})
    if result.matched_count:
        # Se recalcula el promedio acumulado de tiempo para todos los usuarios
        pipeline = [
            {
                "$group": {
                    "_id": None,
                    "total_time": {"$sum": "$totalTime"}
                }
            }
        ]
        agg_result = list(users_collection.aggregate(pipeline))
        total_time_all = agg_result[0]["total_time"] if agg_result else 0
        count_users = users_collection.count_documents({})
        average_time = total_time_all / count_users if count_users else 0

        # Se actualiza el campo average_time en las estadísticas globales
        stats_collection.update_one({"_id": "global"}, {"$set": {"average_time": average_time}})

        return jsonify({
            "message": f"Tiempo de sesión actualizado para el usuario {user_id}.",
            "session_time": session_time,
            "average_time": average_time
        }), 200
    else:
        return jsonify({"error": "Usuario no encontrado."}), 404

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
