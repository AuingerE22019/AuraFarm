const http = require('http');

const options = {
  hostname: 'localhost',
  port: 4200,
  path: '/api/Auth/staff/login',
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
  }
};

const req = http.request(options, (res) => {
  let data = '';
  res.on('data', (chunk) => data += chunk);
  res.on('end', () => {
    console.log('Login Response:', res.statusCode, data);
    if (res.statusCode === 200) {
      const token = JSON.parse(data).access_token; // Wait, it returns 'access_token' or 'token'? Let's check.
      
      const req2Options = {
        hostname: 'localhost',
        port: 4200,
        path: '/api/Registration/staff/members',
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': 'Bearer ' + token
        }
      };
      
      const req2 = http.request(req2Options, (res2) => {
         console.log('PDF Response:', res2.statusCode);
         res2.on('data', () => {});
         res2.on('end', () => console.log('PDF stream ended'));
      });
      req2.write(JSON.stringify({
        firstName: 'Test', lastName: 'User', email: 't@t.com', dateOfBirth: '2000-01-01', phone: '123'
      }));
      req2.end();
    }
  });
});

req.write(JSON.stringify({ Email: 'Admin', Password: 'AdminPassword123!' }));
req.end();
